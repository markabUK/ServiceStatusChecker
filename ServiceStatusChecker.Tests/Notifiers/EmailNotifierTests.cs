using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServiceStatusChecker.Models;
using ServiceStatusChecker.Notifiers;
using Xunit;

namespace ServiceStatusChecker.Tests.Notifiers;

public class EmailNotifierTests
{
    [Fact]
    public void EmailNotifier_Name_IsEmail()
    {
        var notifier = new EmailNotifier(
            Options.Create(new NotificationConfig()),
            NullLogger<EmailNotifier>.Instance);

        notifier.Name.Should().Be("Email");
    }

    [Fact]
    public void EmailNotifier_Handles_ContainsEmail()
    {
        var notifier = new EmailNotifier(
            Options.Create(new NotificationConfig()),
            NullLogger<EmailNotifier>.Instance);

        notifier.Handles.Should().Contain("Email");
    }

    [Fact]
    public async Task NotifyAsync_WhenSmtpServerEmpty_DoesNotThrowAndReturnsSilently()
    {
        // When SmtpServer is blank, the notifier should skip gracefully (no SMTP attempt)
        var config = new NotificationConfig
        {
            Email = new EmailConfig { SmtpServer = string.Empty }
        };

        var notifier = new EmailNotifier(
            Options.Create(config),
            NullLogger<EmailNotifier>.Instance);

        var context = new NotificationContext(
            "MyService", "http://x", false, null, null, null, DateTime.UtcNow, false);

        await notifier.Invoking(n => n.NotifyAsync(context, "Email"))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task NotifyAsync_WhenSmtpServerConfigured_ThrowsWrappedInCatch()
    {
        // When an SMTP server is set but unreachable, the notifier catches and logs — not throws
        var config = new NotificationConfig
        {
            Email = new EmailConfig
            {
                From = "from@example.com",
                To = "to@example.com",
                SmtpServer = "smtp.invalid.nonexistent",
                Port = 587
            }
        };

        var notifier = new EmailNotifier(
            Options.Create(config),
            NullLogger<EmailNotifier>.Instance);

        var context = new NotificationContext(
            "MyService", "http://x", false, null, null, null, DateTime.UtcNow, false);

        // Internal errors are caught and logged — NotifyAsync must not propagate
        await notifier.Invoking(n => n.NotifyAsync(context, "Email"))
            .Should().NotThrowAsync();
    }
}
