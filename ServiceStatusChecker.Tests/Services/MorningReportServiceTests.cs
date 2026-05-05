using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ServiceStatusChecker.Models;
using ServiceStatusChecker.Notifiers;
using ServiceStatusChecker.Services;
using ServiceStatusChecker.State;
using Xunit;

namespace ServiceStatusChecker.Tests.Services;

public class MorningReportServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _stateFilePath;
    private readonly JsonStateStore _stateStore;
    private readonly MorningReportStateStore _reportStateStore;

    public MorningReportServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _stateFilePath = Path.Combine(_tempDir, "state.json");

        var stateOptions = Options.Create(new StateConfig { FilePath = _stateFilePath });
        _stateStore = new JsonStateStore(stateOptions, NullLogger<JsonStateStore>.Instance);
        _reportStateStore = new MorningReportStateStore(stateOptions, NullLogger<MorningReportStateStore>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }

    private MorningReportService CreateService(
        MorningReportConfig? reportConfig = null,
        MonitorConfig[]? monitors = null,
        IEnumerable<INotifier<MorningReportMessageContext>>? notifiers = null)
    {
        reportConfig ??= new MorningReportConfig { Enabled = true };
        monitors ??= Array.Empty<MonitorConfig>();
        notifiers ??= Array.Empty<INotifier<MorningReportMessageContext>>();

        return new MorningReportService(
            _stateStore,
            _reportStateStore,
            Options.Create(new MonitorConfigCollection { MonitorConfig = monitors }),
            Options.Create(reportConfig),
            notifiers,
            NullLogger<MorningReportService>.Instance);
    }

    [Fact]
    public async Task SendIfDueAsync_WhenDisabled_ReturnsFalse()
    {
        var service = CreateService(reportConfig: new MorningReportConfig { Enabled = false });
        var result = await service.SendIfDueAsync();
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendIfDueAsync_WhenAlreadySentToday_ReturnsFalse()
    {
        _reportStateStore.SetLastReportDate(DateOnly.FromDateTime(DateTime.Now));
        var service = CreateService();
        var result = await service.SendIfDueAsync();
        result.Should().BeFalse();
    }

    [Fact]
    public async Task SendIfDueAsync_WhenNotSentToday_ReturnsTrue()
    {
        _reportStateStore.SetLastReportDate(DateOnly.FromDateTime(DateTime.Now).AddDays(-1));

        var monitors = new[] { new MonitorConfig { Name = "Svc", Url = "http://x" } };
        var service = CreateService(
            monitors: monitors,
            reportConfig: new MorningReportConfig
            {
                Enabled = true,
                Notify = Array.Empty<string>() 
            });

        var result = await service.SendIfDueAsync();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SendIfDueAsync_WhenNeverSent_ReturnsTrue()
    {
        var monitors = new[] { new MonitorConfig { Name = "Svc", Url = "http://x" } };
        var service = CreateService(
            monitors: monitors,
            reportConfig: new MorningReportConfig { Enabled = true, Notify = Array.Empty<string>() });

        var result = await service.SendIfDueAsync();
        result.Should().BeTrue();
    }

    [Fact]
    public async Task SendScheduledReportAsync_WhenDisabled_DoesNotThrow()
    {
        var service = CreateService(reportConfig: new MorningReportConfig { Enabled = false });
        await service.Invoking(s => s.SendScheduledReportAsync()).Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendScheduledReportAsync_NoMonitors_DoesNotThrow()
    {
        var service = CreateService(
            monitors: Array.Empty<MonitorConfig>(),
            reportConfig: new MorningReportConfig { Enabled = true, Notify = new[] { "SomeChannel" } });

        await service.Invoking(s => s.SendScheduledReportAsync()).Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendScheduledReportAsync_WithChannel_DispatchesToNotifier()
    {
        // Arrange
        var monitors = new[] { new MonitorConfig { Name = "Svc", Url = "http://svc.example" } };
        var reportConfig = new MorningReportConfig { Enabled = true, Notify = new[] { "TestChannel" } };

        var notifierMock = new Mock<INotifier<MorningReportMessageContext>>();
        notifierMock.Setup(n => n.Handles).Returns(new[] { "TestChannel" });
        notifierMock.Setup(n => n.NotifyAsync(It.IsAny<MorningReportMessageContext>(), "TestChannel"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        var service = CreateService(reportConfig, monitors, new[] { notifierMock.Object });

        // Act
        await service.SendScheduledReportAsync();

        // Assert
        notifierMock.Verify(n => n.NotifyAsync(It.IsAny<MorningReportMessageContext>(), "TestChannel"), Times.Once);
    }

    [Fact]
    public async Task SendScheduledReportAsync_RecordsReportDate()
    {
        var monitors = new[] { new MonitorConfig { Name = "Svc", Url = "http://x" } };
        var service = CreateService(
            monitors: monitors,
            reportConfig: new MorningReportConfig { Enabled = true, Notify = Array.Empty<string>() });

        await service.SendScheduledReportAsync();

        var today = DateOnly.FromDateTime(DateTime.Now);
        _reportStateStore.GetLastReportDate().Should().Be(today);
    }
}