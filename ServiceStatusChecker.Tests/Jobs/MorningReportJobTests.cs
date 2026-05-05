using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Quartz;
using ServiceStatusChecker.Jobs;
using ServiceStatusChecker.Models;
using ServiceStatusChecker.Notifiers;
using ServiceStatusChecker.Services;
using ServiceStatusChecker.State;
using Xunit;

namespace ServiceStatusChecker.Tests.Jobs;

public class MorningReportJobTests
{
    [Fact]
    public async Task Execute_CallsSendScheduledReportAsync()
    {
        // Arrange: Create dummy dependencies to satisfy the base constructor
        var stateConfig = Options.Create(new StateConfig());
        var stateStore = new JsonStateStore(stateConfig, NullLogger<JsonStateStore>.Instance);
        var reportStateStore = new MorningReportStateStore(stateConfig, NullLogger<MorningReportStateStore>.Instance);
        var notifiers = Array.Empty<INotifier<MorningReportMessageContext>>();

        var serviceMock = new Mock<MorningReportService>(
            stateStore,
            reportStateStore,
            Options.Create(new MonitorConfigCollection()),
            Options.Create(new MorningReportConfig()),
            notifiers,
            NullLogger<MorningReportService>.Instance);

        // Setup the virtual method
        serviceMock.Setup(s => s.SendScheduledReportAsync())
            .Returns(Task.CompletedTask)
            .Verifiable();

        var contextMock = new Mock<IJobExecutionContext>();
        
        var job = new MorningReportJob(serviceMock.Object, NullLogger<MorningReportJob>.Instance);

        // Act
        await job.Execute(contextMock.Object);

        // Assert: Verify that the report trigger was fired
        serviceMock.Verify(s => s.SendScheduledReportAsync(), Times.Once);
    }
}