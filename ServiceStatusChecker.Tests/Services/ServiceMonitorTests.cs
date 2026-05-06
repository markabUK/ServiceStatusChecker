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

    private static (Mock<HttpMessageHandler> Handler, IHttpClientFactory Factory) CreateHttpMocks()
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var factoryMock = new Mock<IHttpClientFactory>();
        factoryMock.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(handler.Object));
        return (handler, factoryMock.Object);
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

    // --- EXISTING TESTS ---

    [Fact]
    public async Task ExecuteAsync_UnknownMonitorName_LogsWarningAndDoesNotThrow()
    {
        var (_, factory) = CreateHttpMocks();
        var monitor = CreateMonitor(factory, Array.Empty<MonitorConfig>());
        await monitor.Invoking(m => m.ExecuteAsync("NonExistent")).Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_ServiceReturns200_StoresUpState()
    {
        var (handler, factory) = CreateHttpMocks();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var config = new MonitorConfig { Name = "MyService", Url = "https://example.com", Notify = Array.Empty<string>() };
        var monitor = CreateMonitor(factory, new[] { config });

        await monitor.ExecuteAsync("MyService");
        _stateStore.Get("MyService").Should().Be(ServiceState.Up);
    }

    // --- EXTRA / UPDATED TESTS ---

    [Fact]
    public async Task ExecuteAsync_RetriesOnFailure_AndEventuallySucceeds()
    {
        // Arrange
        var (handler, factory) = CreateHttpMocks();
        var config = new MonitorConfig { Name = "RetryService", Url = "https://example.com", Notify = Array.Empty<string>() };
        
        // Fail twice, then succeed
        handler.Protected()
            .SetupSequence<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var monitor = CreateMonitor(factory, new[] { config });

        // Act
        await monitor.ExecuteAsync("RetryService");

        // Assert
        _stateStore.Get("RetryService").Should().Be(ServiceState.Up);
        handler.Protected().Verify("SendAsync", Times.Exactly(3), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SendsCustomHeaders()
    {
        // Arrange
        var (handler, factory) = CreateHttpMocks();
        var config = new MonitorConfig 
        { 
            Name = "HeaderService", 
            Url = "https://example.com", 
            Headers = new Dictionary<string, string> { { "X-Test-Header", "TestValue" } }
        };

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", 
                ItExpr.Is<HttpRequestMessage>(req => req.Headers.Contains("X-Test-Header")), 
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

        var monitor = CreateMonitor(factory, new[] { config });

        // Act & Assert
        await monitor.Invoking(m => m.ExecuteAsync("HeaderService")).Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_Failure_CapturesResponseBodyInNotification()
    {
        // Arrange
        var (handler, factory) = CreateHttpMocks();
        _stateStore.Set("MyService", ServiceState.Up);
        
        var config = new MonitorConfig 
        { 
            Name = "MyService", 
            Url = "https://example.com", 
            Notify = new[] { "Slack" },
            IncludeResponseBody = true 
        };

        var errorBody = "{\"error\": \"database connection failed\"}";
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent(errorBody) });

        var notifierMock = new Mock<INotifier<NotificationContext>>();
        notifierMock.Setup(n => n.Handles).Returns(new[] { "Slack" });

        var monitor = CreateMonitor(factory, new[] { config }, new[] { notifierMock.Object });

        // Act
        await monitor.ExecuteAsync("MyService");

        // Assert
        notifierMock.Verify(n => n.NotifyAsync(
            It.Is<NotificationContext>(c => c.ResponseBody == errorBody), "Slack"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_SlowNotifier_TimesOutGracefully()
    {
        // Arrange
        var (handler, factory) = CreateHttpMocks();
        _stateStore.Set("MyService", ServiceState.Up);
        var config = new MonitorConfig { Name = "MyService", Url = "https://example.com", Notify = new[] { "SlowChannel" } };

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var notifierMock = new Mock<INotifier<NotificationContext>>();
        notifierMock.Setup(n => n.Handles).Returns(new[] { "SlowChannel" });
        
        // Simulate a delay longer than the 10s CancellationTokenSource in NotifyAsync
        notifierMock.Setup(n => n.NotifyAsync(It.IsAny<NotificationContext>(), It.IsAny<string>()))
            .Returns(Task.Delay(15000)); 

        var monitor = CreateMonitor(factory, new[] { config }, new[] { notifierMock.Object });

        // Act & Assert
        // We ensure the service itself doesn't crash even if a notifier hangs/times out
        await monitor.Invoking(m => m.ExecuteAsync("MyService")).Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_MixedNotifiers_OneFailsDoesNotBlockOthers()
    {
        // Arrange
        var (handler, factory) = CreateHttpMocks();
        _stateStore.Set("MyService", ServiceState.Up);
        var config = new MonitorConfig { Name = "MyService", Url = "https://example.com", Notify = new[] { "BrokenChannel", "WorkingChannel" } };

        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var brokenNotifier = new Mock<INotifier<NotificationContext>>();
        brokenNotifier.Setup(n => n.Handles).Returns(new[] { "BrokenChannel" });
        brokenNotifier.Setup(n => n.NotifyAsync(It.IsAny<NotificationContext>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("I am broken"));

        var workingNotifier = new Mock<INotifier<NotificationContext>>();
        workingNotifier.Setup(n => n.Handles).Returns(new[] { "WorkingChannel" });

        var monitor = CreateMonitor(factory, new[] { config }, new[] { brokenNotifier.Object, workingNotifier.Object });

        // Act
        await monitor.ExecuteAsync("MyService");

        // Assert
        workingNotifier.Verify(n => n.NotifyAsync(It.IsAny<NotificationContext>(), "WorkingChannel"), Times.Once);
    }
}