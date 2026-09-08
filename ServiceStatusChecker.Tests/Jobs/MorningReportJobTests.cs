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
        var serviceMock = new Mock<IMorningReportService>();

        serviceMock.Setup(s => s.SendScheduledReportAsync())
            .Returns(Task.CompletedTask)
            .Verifiable();

        var contextMock = new Mock<IJobExecutionContext>();
        
        var job = new MorningReportJob(serviceMock.Object, NullLogger<MorningReportJob>.Instance);

        await job.Execute(contextMock.Object);

        serviceMock.Verify(s => s.SendScheduledReportAsync(), Times.Once);
    }
}