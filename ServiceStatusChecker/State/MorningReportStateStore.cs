using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ServiceStatusChecker.State;

public class MorningReportStateStore
{
    private readonly string _filePath;
    private readonly ILogger<MorningReportStateStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public MorningReportStateStore(IOptions<StateConfig> options, ILogger<MorningReportStateStore> logger)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(options.Value.FilePath)) ?? ".";
        _filePath = Path.Combine(dir, "morning_report_state.json");
        _logger = logger;
    }

    public DateOnly? GetLastReportDate()
    {
        try
        {
            if (!File.Exists(_filePath))
                return null;

            string json = File.ReadAllText(_filePath);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("lastReportDate", out var val) &&
                DateOnly.TryParse(val.GetString(), out var date))
            {
                return date;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read morning report state from {FilePath}", _filePath);
        }

        return null;
    }

    public void SetLastReportDate(DateOnly date)
    {
        _lock.Wait();
        try
        {
            var obj = new { lastReportDate = date.ToString("yyyy-MM-dd") };
            File.WriteAllText(_filePath, JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save morning report state to {FilePath}", _filePath);
        }
        finally
        {
            _lock.Release();
        }
    }
}