using System;
using System.Collections.Generic;
using FluentAssertions;
using ServiceStatusChecker.Models;
using ServiceStatusChecker.Notifiers;
using ServiceStatusChecker.Notifiers.Formatters;
using ServiceStatusChecker.Services;
using ServiceStatusChecker.State;
using Xunit;

namespace ServiceStatusChecker.Tests.Formatters;

public class JintFormatterTests
{
    // ─── JintWebhookFormatter ──────────────────────────────────────────────────

    [Fact]
    public void JintWebhookFormatter_Name_IsSet()
    {
        var formatter = new JintWebhookFormatter("my-formatter", "'x'");
        formatter.Name.Should().Be("my-formatter");
    }

    [Fact]
    public void JintWebhookFormatter_ExecutesScript_ReturnsFormattedString()
    {
        var context = new NotificationContext(
            "TestService", "https://example.com", false, "timeout", "504", null,
            new DateTime(2025, 5, 5, 8, 0, 0, DateTimeKind.Utc), false);

        var formatter = new JintWebhookFormatter(
            "custom",
            "'Service ' + context.ServiceName + ' is DOWN'");

        var result = formatter.Format(context);
        result.Should().Be("Service TestService is DOWN");
    }

    [Fact]
    public void JintWebhookFormatter_CanAccessIsUp()
    {
        var context = new NotificationContext(
            "Svc", "http://x", true, null, null, null, DateTime.UtcNow, false);

        var formatter = new JintWebhookFormatter(
            "upcheck",
            "context.IsUp ? 'UP' : 'DOWN'");

        formatter.Format(context).Should().Be("UP");
    }

    // ─── JintMorningReportFormatter ────────────────────────────────────────────

    [Fact]
    public void JintMorningReportFormatter_Name_IsSet()
    {
        var formatter = new JintMorningReportFormatter("morning-custom", "'x'");
        formatter.Name.Should().Be("morning-custom");
    }

    [Fact]
    public void JintMorningReportFormatter_ExecutesScript_ReturnsString()
    {
        var context = new MorningReportMessageContext(
            new DateOnly(2025, 5, 5),
            new DateTime(2025, 5, 5, 8, 0, 0, DateTimeKind.Utc),
            new List<MorningReportMonitorSnapshot>
            {
                new("SvcA", "http://a.com", ServiceState.Up)
            });

        var formatter = new JintMorningReportFormatter(
            "custom-morning",
            "'Monitors: ' + context.Monitors.Count");

        var result = formatter.Format(context);
        result.Should().Be("Monitors: 1");
    }
}
