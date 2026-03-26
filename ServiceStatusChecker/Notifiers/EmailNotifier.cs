using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using ServiceStatusChecker.Models;

namespace ServiceStatusChecker.Notifiers;

public class EmailNotifier : INotifier
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

    public async Task NotifyAsync(NotificationContext context, string channel)
    {
        if (string.IsNullOrWhiteSpace(_config.SmtpServer))
        {
            _logger.LogWarning("Email notifier not configured with SMTP server.");
            return;
        }

        try
        {
            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(_config.From));
            email.To.Add(MailboxAddress.Parse(_config.To));

            email.Subject = $"[Monitor] {context.ServiceName} is {(context.IsUp ? "UP" : "DOWN")}";

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

            _logger.LogInformation("Email notification sent for {ServiceName}", context.ServiceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Email notifier failed while sending notification for {ServiceName}",
                context.ServiceName);
        }
    }
}