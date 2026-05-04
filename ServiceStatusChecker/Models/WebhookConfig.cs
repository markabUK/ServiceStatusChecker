namespace ServiceStatusChecker.Models;

public class WebhookConfig
{
    public string WebhookUrl { get; set; } = string.Empty;

    public string? Formatter { get; set; }
}