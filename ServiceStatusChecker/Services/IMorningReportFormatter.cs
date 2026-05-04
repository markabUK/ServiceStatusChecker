namespace ServiceStatusChecker.Services;

public interface IMorningReportFormatter
{
    string Name { get; }
    string Format(string baseMessage);
}