using System;
using FluentAssertions;
using ServiceStatusChecker.Models;
using Xunit;

namespace ServiceStatusChecker.Tests.Models;

public class ModelTests
{
    [Fact]
    public void EmailConfig_Defaults()
    {
        var config = new EmailConfig();
        config.From.Should().BeEmpty();
        config.To.Should().BeEmpty();
        config.SmtpServer.Should().BeEmpty();
        config.Port.Should().Be(587);
        config.UseSsl.Should().BeTrue();
        config.UserName.Should().BeEmpty();
        config.Password.Should().BeEmpty();
    }

    [Fact]
    public void WebhookConfig_Defaults()
    {
        var config = new WebhookConfig();
        config.WebhookUrl.Should().BeEmpty();
        config.Formatter.Should().BeNull();
    }

    [Fact]
    public void NotificationConfig_Defaults()
    {
        var config = new NotificationConfig();
        config.Email.Should().NotBeNull();
        config.Webhooks.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void HealthCheckResult_IsUp_True()
    {
        var result = new HealthCheckResult(true, null, "Status: 200 - OK", null);
        result.IsUp.Should().BeTrue();
        result.Error.Should().BeNull();
        result.StatusCode.Should().Be("Status: 200 - OK");
        result.Body.Should().BeNull();
    }

    [Fact]
    public void HealthCheckResult_IsUp_False()
    {
        var result = new HealthCheckResult(false, "timeout", "504", "body content");
        result.IsUp.Should().BeFalse();
        result.Error.Should().Be("timeout");
        result.StatusCode.Should().Be("504");
        result.Body.Should().Be("body content");
    }

    [Fact]
    public void NotificationContext_Properties()
    {
        var ts = new DateTime(2025, 5, 5, 8, 0, 0, DateTimeKind.Utc);
        var ctx = new NotificationContext("Svc", "http://x", false, "err", "500", "body", ts, true);
        ctx.ServiceName.Should().Be("Svc");
        ctx.Url.Should().Be("http://x");
        ctx.IsUp.Should().BeFalse();
        ctx.Error.Should().Be("err");
        ctx.StatusCode.Should().Be("500");
        ctx.ResponseBody.Should().Be("body");
        ctx.Timestamp.Should().Be(ts);
        ctx.IncludeResponseBody.Should().BeTrue();
    }

    [Fact]
    public void NotificationContext_WithExpression()
    {
        var ts = DateTime.UtcNow;
        var original = new NotificationContext("Svc", "http://x", false, null, null, null, ts, false);
        var modified = original with { IsUp = true, IncludeResponseBody = true };
        modified.IsUp.Should().BeTrue();
        modified.IncludeResponseBody.Should().BeTrue();
        modified.ServiceName.Should().Be("Svc");
    }

    [Fact]
    public void MonitorConfigCollection_Defaults()
    {
        var c = new MonitorConfigCollection();
        c.MonitorConfig.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void MorningReportConfig_Defaults()
    {
        var c = new MorningReportConfig();
        c.Enabled.Should().BeTrue();
        c.Cron.Should().Be("0 0 8 * * ?");
        c.Notify.Should().BeNull();
    }

    [Fact]
    public void GoogleChatConfig_Defaults()
    {
        var c = new GoogleChatConfig();
        c.WebhookUrl.Should().BeEmpty();
    }
}
