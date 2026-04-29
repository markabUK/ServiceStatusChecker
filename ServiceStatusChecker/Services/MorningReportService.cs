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
using ServiceStatusChecker.State;

namespace ServiceStatusChecker.Services;

public class MorningReportService
{
    private readonly JsonStateStore _stateStore;
    private readonly MorningReportStateStore _reportStateStore;
    private readonly MonitorConfigCollection _monitorConfig;
    private readonly NotificationConfig _notificationConfig;
    private readonly MorningReportConfig _reportConfig;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MorningReportService> _logger;

    public MorningReportService(
        JsonStateStore stateStore,
        MorningReportStateStore reportStateStore,
        IOptions<MonitorConfigCollection> monitorOptions,
        IOptions<NotificationConfig> notificationOptions,
        IOptions<MorningReportConfig> reportOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<MorningReportService> logger)
    {
        _stateStore = stateStore;
        _reportStateStore = reportStateStore;
        _monitorConfig = monitorOptions.Value;
        _notificationConfig = notificationOptions.Value;
        _reportConfig = reportOptions.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Sends morning report if not already sent today.
    /// Returns true if a report was sent.
    /// </summary>
    public async Task<bool> SendIfDueAsync()
    {
        if (!_reportConfig.Enabled)
            return false;

        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        DateOnly? lastSent = _reportStateStore.GetLastReportDate();

        if (lastSent == today)
        {
            _logger.LogInformation("Morning report already sent today ({Date}). Skipping.", today);
            return false;
        }

        await SendReportAsync(today);
        return true;
    }

    /// <summary>
    /// Sends morning report unconditionally (called by scheduled job).
    /// </summary>
    public async Task SendScheduledReportAsync()
    {
        if (!_reportConfig.Enabled)
            return;

        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        await SendReportAsync(today);
    }

    private async Task SendReportAsync(DateOnly reportDate)
    {
        _logger.LogInformation("Sending morning state report for {Date}", reportDate);

        var monitors = _monitorConfig.MonitorConfig;
        if (monitors == null || monitors.Length == 0)
        {
            _logger.LogWarning("No monitors configured. Skipping morning report.");
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"📋 Morning Service Status Report — {reportDate:dddd, MMMM d, yyyy}");
        sb.AppendLine(new string('─', 50));

        foreach (var monitor in monitors)
        {
            ServiceState state = _stateStore.Get(monitor.Name);
            string icon = state switch
            {
                ServiceState.Up => "✅",
                ServiceState.Down => "❌",
                _ => "❓"
            };
            sb.AppendLine($"{icon} {monitor.Name}: {state}");
            sb.AppendLine($"   URL: {monitor.Url}");
        }

        sb.AppendLine(new string('─', 50));
        sb.AppendLine($"Generated at: {DateTime.Now:u}");

        string message = sb.ToString();

        var channels = _reportConfig.Notify;
        if (channels == null || channels.Length == 0)
        {
            _logger.LogWarning("No notify channels configured for morning report.");
            _reportStateStore.SetLastReportDate(reportDate);
            return;
        }

        var tasks = new List<Task>();
        foreach (var channel in channels)
        {
            if (_notificationConfig.Webhooks.TryGetValue(channel, out var webhookUrl))
            {
                tasks.Add(SendWebhookAsync(channel, webhookUrl, message));
            }
            else
            {
                _logger.LogWarning("Morning report: no webhook found for channel '{Channel}'", channel);
            }
        }

        await Task.WhenAll(tasks);

        _reportStateStore.SetLastReportDate(reportDate);
        _logger.LogInformation("Morning report sent and state recorded for {Date}", reportDate);
    }

    private async Task SendWebhookAsync(string channel, string url, string message)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            var payload = JsonSerializer.Serialize(new { text = message });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, content);

            if (!response.IsSuccessStatusCode)
                _logger.LogError("Morning report webhook '{Channel}' failed with status {Status}", channel, response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Morning report webhook '{Channel}' threw an exception", channel);
        }
    }
}
