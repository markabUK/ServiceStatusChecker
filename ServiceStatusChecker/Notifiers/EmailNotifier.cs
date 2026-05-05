using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using ServiceStatusChecker.Models;
using ServiceStatusChecker.Services;
using ServiceStatusChecker.State;

namespace ServiceStatusChecker.Notifiers;

public class EmailNotifier : 
    INotifier<NotificationContext>, 
    INotifier<MorningReportMessageContext>
{
    private readonly EmailConfig _config;
    private readonly ILogger<EmailNotifier> _logger;

    public EmailNotifier(IOptions<NotificationConfig> options, ILogger<EmailNotifier> logger)
    {
        _config = options.Value.Email;
        _logger = logger;
    }

    public IReadOnlyCollection<string> Handles { get; } = new[] { "Email" };

    public string Name => "Email";

    // --- Implementation 1: Normal Status Alerts ---
    public async Task NotifyAsync(NotificationContext context, string channel)
    {
        if (string.IsNullOrWhiteSpace(_config.SmtpServer))
        {
            _logger.LogWarning("Email notifier not configured with SMTP server.");
            return;
        }

        string subject = $"[Monitor] {context.ServiceName} is {(context.IsUp ? "UP" : "DOWN")}";

        string bodySection = context.IncludeResponseBody
            ? (context.ResponseBody ?? "(empty)")
            : "**REDACTED**";

        string body = $@"
Service: {context.ServiceName}
URL: {context.Url}
Status: {(context.IsUp ? "UP" : "DOWN")}
Time: {context.Timestamp:u}

Error: {context.Error ?? "None"}
Status Code: {context.StatusCode ?? "No response"}

Response Body:
{bodySection}
";

        await SendEmailAsync(subject, body, context.ServiceName);
    }

    // --- Implementation 2: Morning Reports ---
    public async Task NotifyAsync(MorningReportMessageContext context, string channel)
    {
        if (string.IsNullOrWhiteSpace(_config.SmtpServer))
        {
            _logger.LogWarning("Email notifier not configured with SMTP server.");
            return;
        }

        string subject = $"[Monitor] Morning Service Status Report - {context.ReportDate:yyyy-MM-dd}";

        var sb = new StringBuilder();
        sb.AppendLine($"Morning Service Status Report — {context.ReportDate:dddd, MMMM d, yyyy}");
        sb.AppendLine("Last status of monitored services from yesterday:");
        sb.AppendLine(new string('-', 50));

        foreach (var monitor in context.Monitors)
        {
            string statusText = monitor.State switch
            {
                ServiceState.Up => "UP",
                ServiceState.Down => "DOWN",
                _ => "UNKNOWN"
            };

            sb.AppendLine($"[{statusText}] {monitor.Name}");
            sb.AppendLine($"      URL: {monitor.Url}");
        }

        sb.AppendLine(new string('-', 50));
        sb.AppendLine($"Generated at: {context.GeneratedAtUtc:u}");

        await SendEmailAsync(subject, sb.ToString(), "Morning Report");
    }

    // --- Shared SMTP Helper ---
    private async Task SendEmailAsync(string subject, string body, string logContextName)
    {
        try
        {
            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(_config.From));
            email.To.Add(MailboxAddress.Parse(_config.To));

            email.Subject = subject;
            email.Body = new TextPart("plain") { Text = body };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(
                _config.SmtpServer,
                _config.Port,
                _config.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto
            );

            if (!string.IsNullOrEmpty(_config.UserName))
            {
                await smtp.AuthenticateAsync(_config.UserName, _config.Password);
            }

            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);

            _logger.LogInformation("Email notification sent for {LogContextName}", logContextName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Email notifier failed while sending notification for {LogContextName}",
                logContextName);
        }
    }
}