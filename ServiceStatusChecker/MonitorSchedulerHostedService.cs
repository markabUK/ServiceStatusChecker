using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;
using Quartz.Impl.Matchers;
using ServiceStatusChecker.Jobs;
using ServiceStatusChecker.Models;
using ServiceStatusChecker.Services;

namespace ServiceStatusChecker;

public class MonitorSchedulerHostedService : IHostedService
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IOptionsMonitor<MonitorConfigCollection> _monitorOptions;
    private readonly ILogger<MonitorSchedulerHostedService> _logger;
    private IScheduler? _scheduler;
    private IDisposable? _changeSubscription;
    private readonly MorningReportService _morningReportService;
    private readonly IOptionsMonitor<MorningReportConfig> _morningReportConfig;

    public MonitorSchedulerHostedService(
        ISchedulerFactory schedulerFactory,
        IOptionsMonitor<MonitorConfigCollection> monitorOptions,
        MorningReportService morningReportService,
        IOptionsMonitor<MorningReportConfig> morningReportConfig,
        ILogger<MonitorSchedulerHostedService> logger)
    {
        _schedulerFactory = schedulerFactory;
        _monitorOptions = monitorOptions;
        _morningReportService = morningReportService;
        _morningReportConfig = morningReportConfig;
        _logger = logger;
    }


    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _scheduler = await _schedulerFactory.GetScheduler(cancellationToken);
        await _scheduler.Start(cancellationToken);

        _logger.LogWarning("Scheduler InStandbyMode: {Standby}", _scheduler.InStandbyMode);

        await ApplyConfigAsync(_monitorOptions.CurrentValue, cancellationToken);

        // Schedule the morning report job
        await ScheduleMorningReportAsync(cancellationToken);

        // On startup: send morning report if not yet sent today
        _logger.LogInformation("Checking if morning report is due on startup...");
        await _morningReportService.SendIfDueAsync();

        _changeSubscription = _monitorOptions.OnChange((cfg, _) =>
        {
            _logger.LogInformation("Configuration file changed. Reloading monitors...");
            ApplyConfigAsync(cfg, CancellationToken.None).GetAwaiter().GetResult();
        });
    }

    private async Task ScheduleMorningReportAsync(CancellationToken token)
    {
        if (!_morningReportConfig.CurrentValue.Enabled)
        {
            _logger.LogInformation("Morning report is disabled. Skipping job registration.");
            return;
        }

        string cron = _morningReportConfig.CurrentValue.Cron;

        JobKey jobKey = new JobKey("MorningReport", "Reports");

        IJobDetail job = JobBuilder.Create<MorningReportJob>()
            .WithIdentity(jobKey)
            .Build();

        ITrigger trigger = TriggerBuilder.Create()
            .WithIdentity("MorningReport-trigger", "Reports")
            .WithCronSchedule(cron)
            .ForJob(jobKey)
            .Build();

        await _scheduler!.ScheduleJob(job, trigger, token);
        _logger.LogInformation("Morning report job scheduled with cron: {Cron}", cron);
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
