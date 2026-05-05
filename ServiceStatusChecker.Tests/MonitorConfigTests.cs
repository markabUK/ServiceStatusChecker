// ServiceStatusChecker.Tests/MonitorConfigTests.cs
using Xunit;
using FluentAssertions;
using ServiceStatusChecker.Models;

namespace ServiceStatusChecker.Tests;

public class MonitorConfigTests
{
    [Fact]
    public void MonitorConfig_Defaults()
    {
        // Arrange & Act
        var config = new MonitorConfig();

        // Assert
        config.Name.Should().BeEmpty();
        config.Url.Should().BeEmpty();
        config.IncludeResponseBody.Should().BeFalse();
        config.AllowInsecureHttps.Should().BeFalse();
        config.Headers.Should().BeEmpty();
    }

    [Fact]
    public void MonitorConfig_WithHeaders()
    {
        // Arrange
        var config = new MonitorConfig
        {
            Name = "Test Service",
            Url = "https://example.com/health",
            Headers = new Dictionary<string, string>
            {
                { "Accept", "application/json" },
                { "X-Custom", "value" }
            }
        };

        // Act & Assert
        config.Headers.Should().HaveCount(2);
        config.Headers["Accept"].Should().Be("application/json");
    }
}