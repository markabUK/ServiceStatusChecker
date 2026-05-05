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
using ServiceStatusChecker.Notifiers.Formatters;
using ServiceStatusChecker.Services;

namespace ServiceStatusChecker.Notifiers;

public class WebhookNotifier : 
    INotifier<NotificationContext>, 
    INotifier<MorningReportMessageContext>
{
    private readonly Dictionary<string, WebhookConfig> _webhooks;
    
    // Status Formatters
    private readonly IReadOnlyDictionary<string, IWebhookBodyFormatter> _formatters;
    private readonly IWebhookBodyFormatter _defaultFormatter;
    
    // Morning Report Formatters
    private readonly IReadOnlyDictionary<string, IMorningReportFormatter> _morningFormatters;
    private readonly IMorningReportFormatter _defaultMorningFormatter;
    
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WebhookNotifier> _logger;

    public IReadOnlyCollection<string> Handles { get; }
    public string Name => "Webhook";

    public WebhookNotifier(
        IOptions<NotificationConfig> options,
        IEnumerable<IWebhookBodyFormatter> formatters,
        IEnumerable<IMorningReportFormatter> morningFormatters,
        IHttpClientFactory httpClientFactory,
        ILogger<WebhookNotifier> logger)
    {
        _webhooks = options.Value.Webhooks;

        _formatters = formatters.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        if (!_formatters.TryGetValue("default", out _defaultFormatter!))
            throw new InvalidOperationException("No webhook formatter named 'default' is registered.");

        _morningFormatters = morningFormatters.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        if (!_morningFormatters.TryGetValue("default", out _defaultMorningFormatter!))
            throw new InvalidOperationException("No morning report formatter named 'default' is registered.");

        Handles = _webhooks.Keys.ToArray();
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // --- Implementation 1: Normal Status Alerts ---
    public async Task NotifyAsync(NotificationContext context, string channel)
    {
        if (!TryGetWebhook(channel, out var webhook)) return;

        var formatter = ResolveFormatter(channel, webhook);
        await SendPayloadAsync(channel, webhook.WebhookUrl, formatter.Format(context));
    }

    // --- Implementation 2: Morning Reports ---
    public async Task NotifyAsync(MorningReportMessageContext context, string channel)
    {
        if (!TryGetWebhook(channel, out var webhook)) return;

        var formatter = ResolveMorningFormatter(channel, webhook);
        await SendPayloadAsync(channel, webhook.WebhookUrl, formatter.Format(context));
    }

    // --- Shared Helpers ---
    private bool TryGetWebhook(string channel, out WebhookConfig webhook)
    {
        if (!_webhooks.TryGetValue(channel, out webhook!) || string.IsNullOrWhiteSpace(webhook.WebhookUrl))
        {
            _logger.LogWarning("Webhook '{Channel}' not found or has no URL configured.", channel);
            return false;
        }
        return true;
    }

    private async Task SendPayloadAsync(string channel, string url, string message)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var payload = JsonSerializer.Serialize(new { text = message });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            
            var response = await client.PostAsync(url, content);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Webhook '{Channel}' failed with status {StatusCode}", channel, response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook '{Channel}' threw an exception.", channel);
        }
    }

    private IWebhookBodyFormatter ResolveFormatter(string channel, WebhookConfig webhook)
    {
        if (!string.IsNullOrWhiteSpace(webhook.Formatter) && _formatters.TryGetValue(webhook.Formatter, out var formatter))
            return formatter;
        return _defaultFormatter;
    }

    private IMorningReportFormatter ResolveMorningFormatter(string channel, WebhookConfig webhook)
    {
        if (!string.IsNullOrWhiteSpace(webhook.Formatter) && _morningFormatters.TryGetValue(webhook.Formatter, out var formatter))
            return formatter;
        return _defaultMorningFormatter;
    }
}