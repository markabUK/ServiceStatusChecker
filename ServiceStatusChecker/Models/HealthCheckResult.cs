namespace ServiceStatusChecker.Models;

public record HealthCheckResult(bool IsUp, string? Error, string? StatusCode, string? Body);
