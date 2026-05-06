using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceStatusChecker.Models;
using ServiceStatusChecker.Notifiers;
using ServiceStatusChecker.State;

namespace ServiceStatusChecker.Services;

public class MorningReportService: IMorningReportService
{
    private readonly JsonStateStore _stateStore;
    private readonly MorningReportStateStore _reportStateStore;
    private readonly MonitorConfigCollection _monitorConfig;
    private readonly MorningReportConfig _reportConfig;
    
    // --> CHANGED TO GENERIC INTERFACE
    private readonly IEnumerable<INotifier<MorningReportMessageContext>> _notifiers; 
    private readonly ILogger<MorningReportService> _logger;

    public MorningReportService(
        JsonStateStore stateStore,
        MorningReportStateStore reportStateStore,
        IOptions<MonitorConfigCollection> monitorOptions,
        IOptions<MorningReportConfig> reportOptions,
        IEnumerable<INotifier<MorningReportMessageContext>> notifiers, // --> CHANGED TO GENERIC INTERFACE
        ILogger<MorningReportService> logger)
    {
        _stateStore = stateStore;
        _reportStateStore = reportStateStore;
        _monitorConfig = monitorOptions.Value;
        _reportConfig = reportOptions.Value;
        _notifiers = notifiers;
        _logger = logger;
    }

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

    public virtual async Task SendScheduledReportAsync()
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

        var channels = _reportConfig.Notify;
        if (channels == null || channels.Length == 0)
        {
            _logger.LogWarning("No notify channels configured for morning report.");
            _reportStateStore.SetLastReportDate(reportDate);
            return;
        }

        var monitorSnapshots = monitors.Select(m => new MorningReportMonitorSnapshot(
            m.Name,
            m.Url,
            _stateStore.Get(m.Name)
        )).ToList();

        var reportContext = new MorningReportMessageContext(
            reportDate,
            DateTime.UtcNow,
            monitorSnapshots
        );

        var tasks = new List<Task>();
        foreach (var channel in channels)
        {
            var targetNotifier = _notifiers.FirstOrDefault(n => n.Handles.Contains(channel));
            if (targetNotifier != null)
            {
                tasks.Add(targetNotifier.NotifyAsync(reportContext, channel));
            }
            else
            {
                _logger.LogWarning("No capable notifier found for morning report channel '{Channel}'", channel);
            }
        }

        await Task.WhenAll(tasks);

        _reportStateStore.SetLastReportDate(reportDate);
        _logger.LogInformation("Morning report sent and state recorded for {Date}", reportDate);
    }
}