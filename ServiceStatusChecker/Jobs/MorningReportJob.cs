using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartz;
using ServiceStatusChecker.Services;

namespace ServiceStatusChecker.Jobs;

public class MorningReportJob : IJob
{
    private readonly IMorningReportService _reportService;
    private readonly ILogger<MorningReportJob> _logger;

    public MorningReportJob(IMorningReportService reportService, ILogger<MorningReportJob> logger)
    {
        _reportService = reportService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Executing scheduled morning report job.");
        await _reportService.SendScheduledReportAsync();
    }
}