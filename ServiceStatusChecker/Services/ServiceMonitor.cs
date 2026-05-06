using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using ServiceStatusChecker.Models;
using ServiceStatusChecker.Notifiers;
using ServiceStatusChecker.State;

namespace ServiceStatusChecker.Services;

public class ServiceMonitor : IServiceMonitor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly JsonStateStore _stateStore;
    private readonly IEnumerable<INotifier<NotificationContext>> _notifiers;
    private readonly MonitorConfigCollection _monitorConfigCollection;
    private readonly ILogger<ServiceMonitor> _logger;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public ServiceMonitor(
        IHttpClientFactory httpClientFactory,
        JsonStateStore stateStore,
        IOptions<MonitorConfigCollection> monitorConfigurationCollectionOptions,
        IEnumerable<INotifier<NotificationContext>> notifiers,
        ILogger<ServiceMonitor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _stateStore = stateStore;
        _notifiers = notifiers;
        _monitorConfigCollection = monitorConfigurationCollectionOptions.Value;
        _logger = logger;

        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                3,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                (result, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry {Retry} for monitor {MonitorName}. Delay {Delay}.",
                        retryCount, context["MonitorName"], timespan);
                });
    }

    public virtual async Task ExecuteAsync(string monitorName)
    {
        try
        {
            MonitorConfig? config = _monitorConfigCollection.MonitorConfig?
                .FirstOrDefault(m => m.Name == monitorName);

            if (config == null)
            {
                _logger.LogWarning("No configuration found for monitor {MonitorName}", monitorName);
                return;
            }

            _logger.LogInformation("Running monitor {Name}", config.Name);

            HealthCheckResult result = await CheckHealthAsync(config);
            bool isUp = result.IsUp;

            ServiceState previous = _stateStore.Get(config.Name);
            ServiceState current = isUp ? ServiceState.Up : ServiceState.Down;

            if (current != previous)
            {
                _logger.LogInformation("{Name} transitioned from {Prev} to {Curr}. Sending notification.",
                    config.Name, previous, current);

                await NotifyAsync(config, result, isUp);
            }
            else
            {
                _logger.LogInformation("State unchanged for {Name}: still {State}.", config.Name, current);
            }

            _stateStore.Set(config.Name, current);
        }
        catch (Exception ex)
        {
            // Global safety catch to ensure Quartz doesn't crash the thread
            _logger.LogError(ex, "An unhandled error occurred while executing monitor {Name}", monitorName);
        }
    }

    private async Task<HealthCheckResult> CheckHealthAsync(MonitorConfig config)
    {
        _logger.LogInformation("Checking {Url}", config.Url);
        string clientName = config.AllowInsecureHttps ? "HealthCheckInsecure" : "HealthCheck";
        HttpClient client = _httpClientFactory.CreateClient(clientName);

        try
        {
            HttpResponseMessage response = await _retryPolicy.ExecuteAsync(
                async (context, token) =>
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, config.Url);
                    if (config.Headers != null)
                    {
                        foreach (var header in config.Headers)
                        {
                            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
                            {
                                _logger.LogWarning("Invalid header '{Header}' for monitor {MonitorName}", header.Key, config.Name);
                            }
                        }
                    }
                    return await client.SendAsync(request, token);
                },
                new Context("HealthCheck") { ["MonitorName"] = config.Name },
                CancellationToken.None);

            string? body = null;
            if (!response.IsSuccessStatusCode && config.IncludeResponseBody)
            {
                body = await response.Content.ReadAsStringAsync();
            }

            return new HealthCheckResult(
                response.IsSuccessStatusCode,
                null,
                $"Status: {(int)response.StatusCode} - {response.ReasonPhrase}",
                body
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check failed for {Url}", config.Url);
            return new HealthCheckResult(false, ex.Message, null, null);
        }
    }

    private async Task NotifyAsync(MonitorConfig config, HealthCheckResult result, bool isUp)
    {
        if (config.Notify == null || !config.Notify.Any()) return;

        var context = new NotificationContext(
            ServiceName: config.Name,
            Url: config.Url,
            IsUp: isUp,
            Error: result.Error,
            StatusCode: result.StatusCode,
            ResponseBody: result.Body,
            Timestamp: DateTime.UtcNow,
            IncludeResponseBody: config.IncludeResponseBody
        );

        _logger.LogInformation("Sending notifications for {Name}", config.Name);

        var tasks = config.Notify.Select(async channel =>
        {
            var notifier = _notifiers.FirstOrDefault(n => n.Handles.Contains(channel));

            if (notifier == null)
            {
                _logger.LogWarning("No notifier found for channel '{Channel}'", channel);
                return;
            }

            try
            {
                // Ensure a slow notifier doesn't hang the whole monitoring loop
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                await notifier.NotifyAsync(context, channel).WaitAsync(cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Notifier for {Channel} failed for {ServiceName}", channel, config.Name);
            }
        });

        await Task.WhenAll(tasks);
    }
}