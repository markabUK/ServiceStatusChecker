using System;
using FluentAssertions;
using ServiceStatusChecker.Models;
using ServiceStatusChecker.Notifiers;
using ServiceStatusChecker.Notifiers.Formatters;
using Xunit;

namespace ServiceStatusChecker.Tests.Formatters;

public class WebhookFormatterTests
{
    private readonly NotificationContext _context;

    public WebhookFormatterTests()
    {
        _context = new NotificationContext(
            ServiceName: "Test Service",
            Url: "https://example.com/api",
            IsUp: false,
            Error: "Timeout",
            StatusCode: "504",
            ResponseBody: "{\"error\":\"gateway timeout\"}",
            Timestamp: new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            IncludeResponseBody: true
        );
    }

    [Fact]
    public void DefaultWebhookBodyFormatter_FormatsCorrectly()
    {
        // Arrange
        var formatter = new DefaultWebhookBodyFormatter();

        // Act
        var result = formatter.Format(_context);

        // Assert
        result.Should().Contain("Service: Test Service");
        result.Should().Contain("URL: https://example.com/api");
        result.Should().Contain("Status: DOWN");
        result.Should().Contain("Error: Timeout");
        result.Should().Contain("Status Code: 504");
        result.Should().Contain("{\"error\":\"gateway timeout\"}");
    }

    [Fact]
    public void DefaultWebhookBodyFormatter_RedactsBody_WhenConfigured()
    {
        // Arrange
        var formatter = new DefaultWebhookBodyFormatter();
        var contextWithoutBody = _context with { IncludeResponseBody = false };

        // Act
        var result = formatter.Format(contextWithoutBody);

        // Assert
        result.Should().Contain("**REDACTED**");
        result.Should().NotContain("gateway timeout");
    }

    [Fact]
    public void GoogleChatWebhookBodyFormatter_FormatsCorrectly()
    {
        // Arrange
        var formatter = new GoogleChatWebhookBodyFormatter();

        // Act
        var result = formatter.Format(_context);

        // Assert
        result.Should().Contain("Service: Test Service");
        result.Should().Contain("Endpoint: <https://example.com/api|Test Service endpoint>");
        result.Should().Contain("Status: DOWN");
        result.Should().Contain("{\"error\":\"gateway timeout\"}");
    }
}