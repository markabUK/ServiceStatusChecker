using System.Collections.Generic;

namespace ServiceStatusChecker.Models;

public class NotificationConfig
{
    public EmailConfig Email { get; set; } = new EmailConfig();
    public Dictionary<string, WebhookConfig> Webhooks { get; set; } = new();
}