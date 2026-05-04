using ServiceStatusChecker.Models;

namespace ServiceStatusChecker.Notifiers;

public class GoogleChatWebhookBodyFormatter : IWebhookBodyFormatter
{
    public string Name => "google-chat";

    public string Format(NotificationContext context)
    {
        string bodySection = context.IncludeResponseBody
            ? (context.ResponseBody ?? "(empty)")
            : "**REDACTED**";

        // Short link label for very long URLs.
        string endpoint = $"<{context.Url}|{context.ServiceName} endpoint>";

        return $@"
Service: {context.ServiceName}
Endpoint: {endpoint}
Status: {(context.IsUp ? "UP" : "DOWN")}
Time: {context.Timestamp:u}

Error: {context.Error ?? "None"}
Status Code: {context.StatusCode ?? "No response"}

Response Body:
{bodySection}
";
    }
}