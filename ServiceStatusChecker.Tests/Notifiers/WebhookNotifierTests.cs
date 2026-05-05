using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using ServiceStatusChecker.Models;
using ServiceStatusChecker.Notifiers;
using ServiceStatusChecker.Notifiers.Formatters;
using ServiceStatusChecker.Services;
using Xunit;

namespace ServiceStatusChecker.Tests.Notifiers;

public class WebhookNotifierTests
{
    [Fact]
    public async Task NotifyAsync_WithMorningReportContext_SendsRequest()
    {
        var config = Options.Create(new NotificationConfig
        {
            Webhooks = new Dictionary<string, WebhookConfig>
            {
                { "discord", new WebhookConfig { WebhookUrl = "https://discord.local", Formatter = "default" } }
            }
        });

        var handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK))
            .Verifiable();

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient(handlerMock.Object));

        var statusFormatters = new[] { new DefaultWebhookBodyFormatter() };
        
        var morningFormatterMock = new Mock<IMorningReportFormatter>();
        morningFormatterMock.Setup(m => m.Name).Returns("default");
        morningFormatterMock.Setup(m => m.Format(It.IsAny<MorningReportMessageContext>())).Returns("TEST MORNING MESSAGE");

        var notifier = new WebhookNotifier(config, statusFormatters, new[] { morningFormatterMock.Object }, httpClientFactoryMock.Object, NullLogger<WebhookNotifier>.Instance);

        var context = new MorningReportMessageContext(DateOnly.FromDateTime(DateTime.UtcNow), DateTime.UtcNow, new List<MorningReportMonitorSnapshot>());

        // Act: Call the INotifier<MorningReportMessageContext> interface method
        await notifier.NotifyAsync(context, "discord");

        handlerMock.Protected().Verify(
            "SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Post),
            ItExpr.IsAny<CancellationToken>());
    }
}