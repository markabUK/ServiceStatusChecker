using FluentAssertions;
using ServiceStatusChecker.State;
using Xunit;

namespace ServiceStatusChecker.Tests.State;

public class MonitorStateStoreTests
{
    [Fact]
    public void Get_UnknownService_ReturnsUnknown()
    {
        var store = new MonitorStateStore();
        var state = store.Get("NonExistent");
        state.Should().Be(ServiceState.Unknown);
    }

    [Fact]
    public void SetAndGet_KnownService_ReturnsCorrectState()
    {
        var store = new MonitorStateStore();
        store.Set("Service1", ServiceState.Up);
        store.Set("Service2", ServiceState.Down);

        store.Get("Service1").Should().Be(ServiceState.Up);
        store.Get("Service2").Should().Be(ServiceState.Down);
    }
}