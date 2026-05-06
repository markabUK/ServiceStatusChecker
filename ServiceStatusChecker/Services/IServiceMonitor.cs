using System.Threading.Tasks;

namespace ServiceStatusChecker.Services;

public interface IServiceMonitor
{
    Task ExecuteAsync(string monitorName);
}