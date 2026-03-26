using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;

namespace ServiceStatusChecker;

public class MonitorJob : IJob
{
    private readonly ServiceMonitor _monitor;
    private readonly ILogger<MonitorJob> _logger;

    public MonitorJob(ServiceMonitor monitor, ILogger<MonitorJob> logger)
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