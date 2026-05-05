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

public class MonitorJobTests
{
    [Fact]
    public async Task Execute_ExtractsJobName_AndCallsMonitorExecuteAsync()
    {
        // Arrange: Create dummy dependencies to satisfy the concrete class constructor
        var stateStore = new JsonStateStore(Options.Create(new StateConfig()), NullLogger<JsonStateStore>.Instance);
        var notifiers = Array.Empty<INotifier<NotificationContext>>();
        
        var monitorMock = new Mock<ServiceMonitor>(
            new Mock<System.Net.Http.IHttpClientFactory>().Object,
            stateStore,
            Options.Create(new MonitorConfigCollection()),
            notifiers,
            NullLogger<ServiceMonitor>.Instance);

        // Setup the virtual method to return a completed task and be verifiable
        monitorMock.Setup(m => m.ExecuteAsync("TestMonitorApp"))
            .Returns(Task.CompletedTask)
            .Verifiable();

        // Mock the Quartz Job Context & Job Detail to simulate the injected trigger data
        var jobDetailMock = new Mock<IJobDetail>();
        jobDetailMock.Setup(jd => jd.Key).Returns(new JobKey("TestMonitorApp", "MonitorGroup"));

        var contextMock = new Mock<IJobExecutionContext>();
        contextMock.Setup(c => c.JobDetail).Returns(jobDetailMock.Object);

        var job = new MonitorJob(monitorMock.Object, NullLogger<MonitorJob>.Instance);

        // Act
        await job.Execute(contextMock.Object);

        // Assert: Verify that ExecuteAsync was called exactly once with the correct name
        monitorMock.Verify(m => m.ExecuteAsync("TestMonitorApp"), Times.Once);
    }
}