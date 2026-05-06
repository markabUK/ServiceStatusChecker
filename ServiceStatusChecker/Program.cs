using System;
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
using ServiceStatusChecker.Jobs;
using ServiceStatusChecker.Models;
using ServiceStatusChecker.Notifiers;
using ServiceStatusChecker.Notifiers.Formatters;
using ServiceStatusChecker.Services;
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

                //Notifier body formatters
                services.AddSingleton<IWebhookBodyFormatter, DefaultWebhookBodyFormatter>();
                services.AddSingleton<IWebhookBodyFormatter, GoogleChatWebhookBodyFormatter>();
                
                services.AddSingleton<IMorningReportFormatter, DefaultMorningReportFormatter>();
                services.AddSingleton<IMorningReportFormatter, GoogleChatMorningReportFormatter>();
                
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var dynamicFormattersDir = Path.Combine(appDir, "Scripts");
                if (Directory.Exists(dynamicFormattersDir))
                {
                    foreach (var file in Directory.GetFiles(dynamicFormattersDir, "*.js"))
                    {
                        string name = Path.GetFileNameWithoutExtension(file);
                        string script = File.ReadAllText(file);

                        // Assume scripts ending in '-morning' are morning report formatters
                        if (name.EndsWith("-morning", StringComparison.OrdinalIgnoreCase))
                        {
                            string cleanName = name.Replace("-morning", "", StringComparison.OrdinalIgnoreCase);
                            services.AddSingleton<IMorningReportFormatter>(new JintMorningReportFormatter(cleanName, script));
                        }
                        else
                        {
                            services.AddSingleton<IWebhookBodyFormatter>(new JintWebhookFormatter(name, script));
                        }
                    }
                }
                
                // --> CHANGED: Register Notifiers for specific generic types
                
                // 1. Register concrete classes to exist as singletons
                services.AddSingleton<EmailNotifier>();
                services.AddSingleton<WebhookNotifier>();

                // 2. Bind the status alert interfaces
                services.AddSingleton<INotifier<NotificationContext>>(sp => sp.GetRequiredService<EmailNotifier>());
                services.AddSingleton<INotifier<NotificationContext>>(sp => sp.GetRequiredService<WebhookNotifier>());

                // 3. Bind the morning report interfaces
                services.AddSingleton<INotifier<MorningReportMessageContext>>(sp => sp.GetRequiredService<EmailNotifier>());
                services.AddSingleton<INotifier<MorningReportMessageContext>>(sp => sp.GetRequiredService<WebhookNotifier>());

                // Monitor + Job
                services.AddTransient<IServiceMonitor, ServiceMonitor>();
                services.AddTransient<MonitorJob>();
                
                services.Configure<MorningReportConfig>(configuration.GetSection("MorningReport"));
                services.AddSingleton<MorningReportStateStore>();
                services.AddTransient<IMorningReportService, MorningReportService>();
                services.AddTransient<MorningReportJob>();

                // QUARTZ 
                services.AddQuartz(q =>
                {
                    q.SetProperty("quartz.threadPool.threadCount", "50"); 
                });
                services.AddQuartzHostedService(options => { options.WaitForJobsToComplete = true; });

                // Your scheduler service
                services.AddHostedService<MonitorSchedulerHostedService>();
            });
    }
}