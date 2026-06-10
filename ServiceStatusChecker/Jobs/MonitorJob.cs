using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using ServiceStatusChecker.Services;

namespace ServiceStatusChecker.Jobs;

public class MonitorJob : IJob
{
    private readonly IServiceMonitor _monitor;
    private readonly ILogger<MonitorJob> _logger;

    public MonitorJob(IServiceMonitor monitor, ILogger<MonitorJob> logger)
    {
        _monitor = monitor;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        string monitorName = context.JobDetail.Key.Name;
        _logger.LogInformation("Executing monitor job {MonitorName}", monitorName);
        await _monitor.ExecuteAsync(monitorName);
    }
}