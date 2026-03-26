using System;

namespace ServiceStatusChecker.Models;

public class MonitorConfigCollection
{
    public MonitorConfig[]? MonitorConfig { get; set; } = Array.Empty<MonitorConfig>();
}