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
    private readonly Dictionary<string, WebhookConfig> _webhooks;
    private readonly IReadOnlyDictionary<string, IWebhookBodyFormatter> _formatters;
    private readonly IWebhookBodyFormatter _defaultFormatter;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookNotifier> _logger;

    public IReadOnlyCollection<string> Handles { get; }

    public WebhookNotifier(
        IOptions<NotificationConfig> options,
        IEnumerable<IWebhookBodyFormatter> formatters,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookNotifier> logger)
    {
        _webhooks = options.Value.Webhooks;

        _formatters = formatters.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        if (!_formatters.TryGetValue("default", out _defaultFormatter!))
            throw new InvalidOperationException("No webhook formatter named 'default' is registered.");

        Handles = _webhooks.Keys.ToArray();
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Name => "Webhook";

    public async Task NotifyAsync(NotificationContext context, string channel)
    {
        if (!_webhooks.TryGetValue(channel, out var webhook))
        {
            _logger.LogWarning("Webhook '{WebhookName}' not found in configuration.", channel);
            return;
        }

        if (string.IsNullOrWhiteSpace(webhook.WebhookUrl))
        {
            _logger.LogWarning("Webhook '{WebhookName}' has no configured URL.", channel);
            return;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            var formatter = ResolveFormatter(channel, webhook);
            string message = formatter.Format(context);

            var payload = JsonSerializer.Serialize(new { text = message });

            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(webhook.WebhookUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Webhook '{WebhookName}' failed with status {StatusCode}",
                    channel, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Webhook '{WebhookName}' threw an exception while notifying {ServiceName}",
                channel, context.ServiceName);
        }
    }

    private IWebhookBodyFormatter ResolveFormatter(string channel, WebhookConfig webhook)
    {
        if (string.IsNullOrWhiteSpace(webhook.Formatter))
            return _defaultFormatter;

        if (_formatters.TryGetValue(webhook.Formatter, out var formatter))
            return formatter;

        _logger.LogWarning(
            "Webhook formatter '{Formatter}' not found for channel '{Channel}'. Falling back to default.",
            webhook.Formatter, channel);

        return _defaultFormatter;
    }
}
