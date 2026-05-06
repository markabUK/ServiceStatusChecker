using System.Threading.Tasks;

namespace ServiceStatusChecker.Services;

public interface IMorningReportService
{
    Task<bool> SendIfDueAsync();
    Task SendScheduledReportAsync();
}
