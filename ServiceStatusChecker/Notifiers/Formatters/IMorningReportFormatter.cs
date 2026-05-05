
using ServiceStatusChecker.Models;
using ServiceStatusChecker.Services;

namespace ServiceStatusChecker.Notifiers.Formatters;

public interface IMorningReportFormatter
{
    string Name { get; }
    string Format(MorningReportMessageContext context);
}
