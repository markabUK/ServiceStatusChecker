namespace ServiceStatusChecker.Models;

public class MorningReportConfig
{
    public bool Enabled { get; set; } = true;
    public string Cron { get; set; } = "0 0 8 * * ?"; // 8:00 AM daily
    public string[]? Notify { get; set; }
}
