using ServiceStatusChecker.Models;

namespace ServiceStatusChecker.Notifiers;

public interface IWebhookBodyFormatter
{
    string Name { get; }
    string Format(NotificationContext context);
}