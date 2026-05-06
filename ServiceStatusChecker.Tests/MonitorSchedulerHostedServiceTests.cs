using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Quartz;
using Quartz.Impl.Matchers;
using ServiceStatusChecker.Models;
using ServiceStatusChecker.Services;
using Xunit;

namespace ServiceStatusChecker.Tests;

public class MonitorSchedulerHostedServiceTests
{
    private readonly Mock<ISchedulerFactory> _schedulerFactoryMock;
    private readonly Mock<IScheduler> _schedulerMock;
    private readonly Mock<IOptionsMonitor<MonitorConfigCollection>> _monitorOptionsMock;
    private readonly Mock<IOptionsMonitor<MorningReportConfig>> _morningReportOptionsMock;
    private readonly Mock<IMorningReportService> _morningReportServiceMock;
    private readonly Mock<ILogger<MonitorSchedulerHostedService>> _loggerMock;

    public MonitorSchedulerHostedServiceTests()
    {
        _schedulerFactoryMock = new Mock<ISchedulerFactory>();
        _schedulerMock = new Mock<IScheduler>();
        _monitorOptionsMock = new Mock<IOptionsMonitor<MonitorConfigCollection>>();
        _morningReportOptionsMock = new Mock<IOptionsMonitor<MorningReportConfig>>();
        _morningReportServiceMock = new Mock<IMorningReportService>();
        _loggerMock = new Mock<ILogger<MonitorSchedulerHostedService>>();

        // 1. Default Monitor Config (Empty but not null)
        _monitorOptionsMock.Setup(m => m.CurrentValue)
            .Returns(new MonitorConfigCollection { MonitorConfig = Array.Empty<MonitorConfig>() });

        // 2. Default Morning Report Config (Valid Cron required for Quartz TriggerBuilder)
        _morningReportOptionsMock.Setup(m => m.CurrentValue)
            .Returns(new MorningReportConfig 
            { 
                Enabled = true, 
                Cron = "0 0 9 * * ?", 
                Notify = new[] { "default-channel" } 
            });

        // 3. Scheduler Factory setup
        _schedulerFactoryMock
            .Setup(f => f.GetScheduler(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_schedulerMock.Object);

        // 4. FIX: ScheduleJob must return a DateTimeOffset, otherwise Quartz/Moq may throw
        _schedulerMock
            .Setup(s => s.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DateTimeOffset.Now);

        // 5. FIX: GetTrigger must return a Mock Trigger to prevent NRE in the Logger (.ToLocalTime() call)
        var mockTrigger = new Mock<ITrigger>();
        mockTrigger.Setup(t => t.GetNextFireTimeUtc()).Returns(DateTimeOffset.UtcNow);
        
        _schedulerMock
            .Setup(s => s.GetTrigger(It.IsAny<TriggerKey>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockTrigger.Object);
        
        // 6. Default JobKeys setup
        _schedulerMock.Setup(s => s.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JobKey>().AsReadOnly());
    }

    [Fact]
    public async Task StartAsync_SchedulesMonitors_AndSendsInitialReport()
    {
        // Arrange
        var config = new MonitorConfigCollection
        {
            MonitorConfig = new[]
            {
                new MonitorConfig { Name = "TestApp", Url = "http://test.com", Cron = "0/5 * * * * ?", Notify = new[] {"c1"} }
            }
        };
        _monitorOptionsMock.Setup(m => m.CurrentValue).Returns(config);

        var service = CreateService();

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        _schedulerMock.Verify(s => s.Start(It.IsAny<CancellationToken>()), Times.Once);
        
        _schedulerMock.Verify(s => s.ScheduleJob(
            It.Is<IJobDetail>(j => j.Key.Name == "TestApp"), 
            It.IsAny<ITrigger>(), 
            It.IsAny<CancellationToken>()), Times.Once);

        _morningReportServiceMock.Verify(m => m.SendIfDueAsync(), Times.Once);
    }

    [Fact]
    public async Task ConfigurationChange_ReloadsMonitors()
    {
        // Arrange
        Action<MonitorConfigCollection, string?>? onChangeAction = null;
        
        // Capture the OnChange subscription callback
        _monitorOptionsMock
            .Setup(m => m.OnChange(It.IsAny<Action<MonitorConfigCollection, string?>>()))
            .Callback<Action<MonitorConfigCollection, string?>>(action => onChangeAction = action)
            .Returns(new Mock<IDisposable>().Object);

        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        // Prepare the new configuration
        var newConfig = new MonitorConfigCollection
        {
            MonitorConfig = new[] 
            { 
                new MonitorConfig 
                { 
                    Name = "NewApp", 
                    Url = "http://new.com", 
                    Cron = "0 0 * * * ?",
                    Notify = new[] { "slack" }
                } 
            }
        };

        // Act - Simulate the file change event
        onChangeAction?.Invoke(newConfig, "unused");

        // Assert
        
        // Check if an error was logged inside the catch block (if this fails, check console output)
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
            Times.Never, "The ApplyConfigAsync method crashed during the configuration reload.");

        // Verify the old job keys were fetched for deletion
        _schedulerMock.Verify(s => s.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()), Times.AtLeast(2));
    
        // Verify the NEW job was successfully scheduled
        _schedulerMock.Verify(s => s.ScheduleJob(
            It.Is<IJobDetail>(j => j.Key.Name == "NewApp"), 
            It.IsAny<ITrigger>(), 
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopAsync_ShutsDownScheduler()
    {
        // Arrange
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        // Act
        await service.StopAsync(CancellationToken.None);

        // Assert
        _schedulerMock.Verify(s => s.Shutdown(true, It.IsAny<CancellationToken>()), Times.Once);
    }

    private MonitorSchedulerHostedService CreateService()
    {
        return new MonitorSchedulerHostedService(
            _schedulerFactoryMock.Object,
            _monitorOptionsMock.Object,
            _morningReportServiceMock.Object,
            _morningReportOptionsMock.Object,
            _loggerMock.Object);
    }
}