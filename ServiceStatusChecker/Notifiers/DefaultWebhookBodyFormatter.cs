using ServiceStatusChecker.Models;

namespace ServiceStatusChecker.Notifiers;

public class DefaultWebhookBodyFormatter : IWebhookBodyFormatter
{
    public string Name => "default";

    public string Format(NotificationContext context)
    {
        string bodySection = context.IncludeResponseBody
            ? (context.ResponseBody ?? "(empty)")
            : "**REDACTED**";

        return $@"
Service: {context.ServiceName}
URL: {context.Url}
Status: {(context.IsUp ? "UP" : "DOWN")}
Time: {context.Timestamp:u}

Error: {context.Error ?? "None"}
Status Code: {context.StatusCode ?? "No response"}

Response Body:
{bodySection}
";
    }
}