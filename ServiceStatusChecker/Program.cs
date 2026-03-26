using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using ServiceStatusChecker.Models;
using ServiceStatusChecker.Notifiers;
using ServiceStatusChecker.State;

namespace ServiceStatusChecker;

public static class Program
{
    public static async Task Main(string[] args)
    {
        IHost host = CreateHostBuilder(args).Build();
        await host.RunAsync();
    }

    private static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory());
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                IConfiguration configuration = context.Configuration;

                // Bind config sections
                services.Configure<MonitorConfigCollection>(configuration.GetSection("Monitors"));
                services.Configure<NotificationConfig>(configuration.GetSection("Notifications"));
                services.Configure<StateConfig>(configuration.GetSection("State"));

                // Logging
                services.AddLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddConsole();
                    builder.SetMinimumLevel(LogLevel.Information);
                });
                services.Configure<JsonSerializerOptions>(options =>
                {
                    options.Converters.Add(new JsonStringEnumConverter());
                });

                services.AddHttpClient("HealthCheck");

                services.AddHttpClient("HealthCheckInsecure")
                    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback =
                            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    });

                // State store
                services.AddSingleton<JsonStateStore>();

                // Notifiers
                services.AddSingleton<INotifier, EmailNotifier>();
                services.AddSingleton<INotifier, WebhookNotifier>();

                // Monitor + Job
                services.AddTransient<ServiceMonitor>();
                services.AddTransient<MonitorJob>();

                // QUARTZ 
                services.AddQuartz();
                services.AddQuartzHostedService(options => { options.WaitForJobsToComplete = true; });

                // Your scheduler service
                services.AddHostedService<MonitorSchedulerHostedService>();
            });
    }
}