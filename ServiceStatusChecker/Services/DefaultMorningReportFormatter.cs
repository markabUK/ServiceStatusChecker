namespace ServiceStatusChecker.Services;

public class DefaultMorningReportFormatter : IMorningReportFormatter
{
    public string Name => "default";

    public string Format(string baseMessage) => baseMessage;
}
