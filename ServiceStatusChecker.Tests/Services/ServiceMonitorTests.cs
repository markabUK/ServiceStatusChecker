using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using ServiceStatusChecker.Models;
using ServiceStatusChecker.Notifiers;
using ServiceStatusChecker.Services;
using ServiceStatusChecker.State;
using Xunit;

namespace ServiceStatusChecker.Tests.Services;

public class ServiceMonitorTests : IDisposable
{
    private readonly string _tempFile;
    private readonly JsonStateStore _stateStore;

    public ServiceMonitorTests()
    {
        _tempFile = Path.GetTempFileName();
        _stateStore = new JsonStateStore(
            Options.Create(new StateConfig { FilePath = _tempFile }),
            NullLogger<JsonStateStore>.Instance);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    private static IHttpClientFactory CreateHttpClientFactory(HttpStatusCode statusCode)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode));

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler.Object));
        return factoryMock.Object;
    }

    private static IHttpClientFactory CreateFailingHttpClientFactory()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Connection refused (test)"));

        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler.Object));
        return factoryMock.Object;
    }

    private ServiceMonitor CreateMonitor(
        IHttpClientFactory factory,
        MonitorConfig[] configs,
        IEnumerable<INotifier<NotificationContext>>? notifiers = null)
    {
        return new ServiceMonitor(
            factory,
            _stateStore,
            Options.Create(new MonitorConfigCollection { MonitorConfig = configs }),
            notifiers ?? Array.Empty<INotifier<NotificationContext>>(),
            NullLogger<ServiceMonitor>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownMonitorName_LogsWarningAndDoesNotThrow()
    {
        var factory = CreateHttpClientFactory(HttpStatusCode.OK);
        var monitor = CreateMonitor(factory, Array.Empty<MonitorConfig>());

        await monitor.Invoking(m => m.ExecuteAsync("NonExistent")).Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_ServiceReturns200_StoresUpState()
    {
        var config = new MonitorConfig { Name = "MyService", Url = "https://example.com", Notify = Array.Empty<string>() };
        var monitor = CreateMonitor(CreateHttpClientFactory(HttpStatusCode.OK), new[] { config });

        await monitor.ExecuteAsync("MyService");
        _stateStore.Get("MyService").Should().Be(ServiceState.Up);
    }

    [Fact]
    public async Task ExecuteAsync_ServiceFails_StoresDownState()
    {
        var config = new MonitorConfig { Name = "MyService", Url = "https://example.com", Notify = Array.Empty<string>() };
        var monitor = CreateMonitor(CreateFailingHttpClientFactory(), new[] { config });

        await monitor.ExecuteAsync("MyService");
        _stateStore.Get("MyService").Should().Be(ServiceState.Down);
    }

    [Fact]
    public async Task ExecuteAsync_TransitionUpToDown_NotifiesChannel()
    {
        var config = new MonitorConfig { Name = "MyService", Url = "https://example.com", Notify = new[] { "AlertChannel" } };
        _stateStore.Set("MyService", ServiceState.Up);

        var notifierMock = new Mock<INotifier<NotificationContext>>();
        notifierMock.Setup(n => n.Handles).Returns(new[] { "AlertChannel" });
        notifierMock.Setup(n => n.NotifyAsync(It.IsAny<NotificationContext>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        var monitor = CreateMonitor(CreateFailingHttpClientFactory(), new[] { config }, new[] { notifierMock.Object });

        await monitor.ExecuteAsync("MyService");

        notifierMock.Verify(n => n.NotifyAsync(
            It.Is<NotificationContext>(c => c.ServiceName == "MyService" && !c.IsUp), "AlertChannel"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_TransitionDownToUp_NotifiesChannel()
    {
        var config = new MonitorConfig { Name = "MyService", Url = "https://example.com", Notify = new[] { "AlertChannel" } };
        _stateStore.Set("MyService", ServiceState.Down);

        var notifierMock = new Mock<INotifier<NotificationContext>>();
        notifierMock.Setup(n => n.Handles).Returns(new[] { "AlertChannel" });
        notifierMock.Setup(n => n.NotifyAsync(It.IsAny<NotificationContext>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        var monitor = CreateMonitor(CreateHttpClientFactory(HttpStatusCode.OK), new[] { config }, new[] { notifierMock.Object });

        await monitor.ExecuteAsync("MyService");

        notifierMock.Verify(n => n.NotifyAsync(
            It.Is<NotificationContext>(c => c.ServiceName == "MyService" && c.IsUp), "AlertChannel"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_StateUnchangedDown_DoesNotNotify()
    {
        var config = new MonitorConfig { Name = "MyService", Url = "https://example.com", Notify = new[] { "AlertChannel" } };
        _stateStore.Set("MyService", ServiceState.Down);

        var notifierMock = new Mock<INotifier<NotificationContext>>();
        notifierMock.Setup(n => n.Handles).Returns(new[] { "AlertChannel" });

        var monitor = CreateMonitor(CreateFailingHttpClientFactory(), new[] { config }, new[] { notifierMock.Object });

        await monitor.ExecuteAsync("MyService");
        notifierMock.Verify(n => n.NotifyAsync(It.IsAny<NotificationContext>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_NoNotifyChannels_DoesNotCallAnyNotifier()
    {
        var config = new MonitorConfig { Name = "MyService", Url = "https://example.com", Notify = null };
        _stateStore.Set("MyService", ServiceState.Up);

        var notifierMock = new Mock<INotifier<NotificationContext>>();
        notifierMock.Setup(n => n.Handles).Returns(new[] { "SomeChannel" });

        var monitor = CreateMonitor(CreateFailingHttpClientFactory(), new[] { config }, new[] { notifierMock.Object });

        await monitor.ExecuteAsync("MyService");
        notifierMock.Verify(n => n.NotifyAsync(It.IsAny<NotificationContext>(), It.IsAny<string>()), Times.Never);
    }
}