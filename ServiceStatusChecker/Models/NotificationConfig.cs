using System.Collections.Generic;

namespace ServiceStatusChecker.Models;

public class NotificationConfig
{
    public EmailConfig Email { get; set; } = new EmailConfig();
    public Dictionary<string, string> Webhooks { get; set; } = new();
}