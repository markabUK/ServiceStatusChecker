using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Impl.Matchers;
using ServiceStatusChecker.Models;

namespace ServiceStatusChecker;

public class MonitorSchedulerHostedService : IHostedService
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IOptionsMonitor<MonitorConfigCollection> _monitorOptions;
    private readonly ILogger<MonitorSchedulerHostedService> _logger;
    private IScheduler? _scheduler;
    private IDisposable? _changeSubscription;

    public MonitorSchedulerHostedService(
        ISchedulerFactory schedulerFactory,
        IOptionsMonitor<MonitorConfigCollection> monitorOptions,
        ILogger<MonitorSchedulerHostedService> logger)
    {
        _schedulerFactory = schedulerFactory;
        _monitorOptions = monitorOptions;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        await _scheduler.Start(cancellationToken);
        
        _logger.LogWarning("Scheduler InStandbyMode: {Standby}", _scheduler.InStandbyMode);
        

        await ApplyConfigAsync(_monitorOptions.CurrentValue, cancellationToken);

        _changeSubscription = _monitorOptions.OnChange((cfg, _) =>
        {
            _logger.LogInformation("Configuration file changed. Reloading monitors...");
            // Block here to avoid fire-and-forget races
            ApplyConfigAsync(cfg, CancellationToken.None).GetAwaiter().GetResult();
        });
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_changeSubscription != null)
        {
            _changeSubscription.Dispose();
        }

        if (_scheduler != null)
        {
            await _scheduler.Shutdown(waitForJobsToComplete: true, cancellationToken);
        }
    }

    private async Task ApplyConfigAsync(MonitorConfigCollection config, CancellationToken token)
    {
        if (_scheduler == null)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Applying monitor configuration...");

            // Remove existing jobs
            IReadOnlyCollection<JobKey> existingJobs =
                await _scheduler.GetJobKeys(GroupMatcher<JobKey>.AnyGroup(), token);

            if (existingJobs.Count > 0)
            {
                _logger.LogInformation("Removing {Count} existing jobs...", existingJobs.Count);
                await _scheduler.DeleteJobs(new List<JobKey>(existingJobs), token);
            }

            if (config.MonitorConfig == null || config.MonitorConfig.Length == 0)
            {
                _logger.LogWarning("No monitors found in configuration.");
                return;
            }

            // Add new jobs
            foreach (MonitorConfig monitor in config.MonitorConfig)
            {
                _logger.LogInformation(
                    "Adding monitor: {Name} | URL: {Url} | Cron: {Cron} | Notifiers: {Notifiers}",
                    monitor.Name,
                    monitor.Url,
                    monitor.Cron,
                    monitor.Notify != null ? string.Join(", ", monitor.Notify) : "(none)");

                JobKey jobKey = new JobKey(monitor.Name);

                IJobDetail job = JobBuilder.Create<MonitorJob>()
                    .WithIdentity(jobKey)
                    .Build();

                ITrigger trigger = TriggerBuilder.Create()
                    .WithIdentity(monitor.Name + "-trigger")
                    .WithCronSchedule(monitor.Cron, x => x.WithMisfireHandlingInstructionDoNothing())
                    .ForJob(jobKey)
                    .Build();

                await _scheduler.ScheduleJob(job, trigger, token);
                
                var scheduledTrigger = await _scheduler.GetTrigger(trigger.Key, token);
                
                _logger.LogInformation("Next fire time for {Name}: {Next}",
                    monitor.Name,
                    scheduledTrigger?.GetNextFireTimeUtc()?.ToLocalTime());

                _logger.LogInformation("Scheduled job {Name} with trigger {Trigger}",
                    monitor.Name, monitor.Cron);
            }

            _logger.LogInformation("Monitor configuration applied successfully. Total monitors: {Count}",
                config.MonitorConfig.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply monitor configuration.");
        }
    }
}
