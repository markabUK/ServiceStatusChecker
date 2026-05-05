using ServiceStatusChecker.Models;

namespace ServiceStatusChecker.Notifiers.Formatters;

public interface IWebhookBodyFormatter
{
    string Name { get; }
    string Format(NotificationContext context);
}