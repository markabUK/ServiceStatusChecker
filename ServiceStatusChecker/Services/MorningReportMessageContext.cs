// ServiceStatusChecker/Services/MorningReportMessageContext.cs
using System;
using System.Collections.Generic;
using ServiceStatusChecker.State;

namespace ServiceStatusChecker.Services;

public record MorningReportMonitorSnapshot(
    string Name,
    string Url,
    ServiceState State
);

public record MorningReportMessageContext(
    DateOnly ReportDate,
    DateTime GeneratedAtUtc,
    IReadOnlyList<MorningReportMonitorSnapshot> Monitors
);