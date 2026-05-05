using System;
using System.Collections.Generic;
using FluentAssertions;
using ServiceStatusChecker.Models;
using ServiceStatusChecker.Notifiers.Formatters;
using ServiceStatusChecker.Services;
using ServiceStatusChecker.State;
using Xunit;

namespace ServiceStatusChecker.Tests.Formatters;

public class MorningReportFormatterTests
{
    private readonly MorningReportMessageContext _context;

    public MorningReportFormatterTests()
    {
        _context = new MorningReportMessageContext(
            ReportDate: new DateOnly(2023, 10, 1),
            GeneratedAtUtc: new DateTime(2023, 10, 1, 8, 0, 0, DateTimeKind.Utc),
            Monitors: new List<MorningReportMonitorSnapshot>
            {
                new("Service A", "http://a.com", ServiceState.Up),
                new("Service B", "http://b.com", ServiceState.Down),
                new("Service C", "http://c.com", ServiceState.Unknown)
            }
        );
    }

    [Fact]
    public void DefaultMorningReportFormatter_FormatsCorrectly()
    {
        var formatter = new DefaultMorningReportFormatter();
        var result = formatter.Format(_context);

        result.Should().Contain("Morning Service Status Report — Sunday, October 1, 2023");
        result.Should().Contain("✅ Service A: Up");
        result.Should().Contain("❌ Service B: Down");
        result.Should().Contain("❓ Service C: Unknown");
    }

    [Fact]
    public void GoogleChatMorningReportFormatter_FormatsCorrectly()
    {
        var formatter = new GoogleChatMorningReportFormatter();
        var result = formatter.Format(_context);

        result.Should().Contain("Morning Service Status Report - Sunday, October 1, 2023");
        result.Should().Contain("OK Service A: Up");
        result.Should().Contain("<http://a.com|Service A endpoint>");
        result.Should().Contain("DOWN Service B: Down");
        result.Should().Contain("UNKNOWN Service C: Unknown");
    }
}