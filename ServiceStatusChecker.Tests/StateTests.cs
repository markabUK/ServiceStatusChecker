// ServiceStatusChecker.Tests/StateTests.cs
using Xunit;
using FluentAssertions;
using ServiceStatusChecker.State;

namespace ServiceStatusChecker.Tests;

public class ServiceStateTests
{
    [Fact]
    public void ServiceState_EnumValues()
    {
        // Assert
        ServiceState.Unknown.Should().Be(ServiceState.Unknown);
        ServiceState.Up.Should().Be(ServiceState.Up);
        ServiceState.Down.Should().Be(ServiceState.Down);
    }
}