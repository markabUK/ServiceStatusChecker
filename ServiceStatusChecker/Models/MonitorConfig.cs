using System.Collections.Generic;

namespace ServiceStatusChecker.Models;

public class MonitorConfig
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Cron { get; set; } = string.Empty;
    public string[]? Notify { get; set; }
    public bool IncludeResponseBody { get; set; } = false;
    public bool AllowInsecureHttps { get; set; } = false;
    public Dictionary<string, string> Headers { get; set; } = new();
}