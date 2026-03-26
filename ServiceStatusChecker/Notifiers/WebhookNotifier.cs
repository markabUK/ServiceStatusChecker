using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceStatusChecker.Models;

namespace ServiceStatusChecker.Notifiers;

public class WebhookNotifier : INotifier
{
    private readonly Dictionary<string, string> _webhooks;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookNotifier> _logger;

    public IReadOnlyCollection<string> Handles { get; }

    public WebhookNotifier(
        IOptions<NotificationConfig> options,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookNotifier> logger)
    {
        _webhooks = options.Value.Webhooks;
        Handles = _webhooks.Keys.ToArray();
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Name => "Webhook";

    public async Task NotifyAsync(NotificationContext context, string channel)
    {
        if (!_webhooks.TryGetValue(channel, out var url))
        {
            _logger.LogWarning("Webhook '{WebhookName}' not found in configuration.", channel);
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();

            string bodySection = context.IncludeResponseBody
                ? (context.ResponseBody ?? "(empty)")
                : "**REDACTED**";

            string message = $@"
Service: {context.ServiceName}
URL: {context.Url}
Status: {(context.IsUp ? "UP" : "DOWN")}
Time: {context.Timestamp:u}

Error: {context.Error ?? "None"}
Status Code: {context.StatusCode ?? "No response"}

Response Body:
{bodySection}
";


            var payload = JsonSerializer.Serialize(new
            {
                text = message
            });

            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Webhook '{WebhookName}' failed with status {StatusCode}",
                    channel, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Webhook '{WebhookName}' threw an exception while notifying {ServiceName}",
                channel, context.ServiceName);
        }
    }
}
