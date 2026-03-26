namespace ServiceStatusChecker.Models;

public class SmsWebhookConfig : WebhookConfig
{
    public string PhoneNumber { get; set; } = string.Empty;
}