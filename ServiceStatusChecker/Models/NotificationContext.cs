using System;

namespace ServiceStatusChecker.Models;

public record NotificationContext(
    string ServiceName,
    string Url,
    bool IsUp,
    string? Error,
    string? StatusCode,
    string? ResponseBody,
    DateTime Timestamp,
    bool IncludeResponseBody
);