using System;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServiceStatusChecker.State;
using Xunit;

namespace ServiceStatusChecker.Tests.State;

public class MorningReportStateStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _stateFilePath;

    public MorningReportStateStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
        _stateFilePath = Path.Combine(_tempDir, "state.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
    }

    private MorningReportStateStore CreateStore()
    {
        var options = Options.Create(new StateConfig { FilePath = _stateFilePath });
        return new MorningReportStateStore(options, NullLogger<MorningReportStateStore>.Instance);
    }

    [Fact]
    public void GetLastReportDate_WhenNoFile_ReturnsNull()
    {
        var store = CreateStore();
        store.GetLastReportDate().Should().BeNull();
    }

    [Fact]
    public void SetAndGet_PersistsDate()
    {
        var date = new DateOnly(2025, 5, 5);
        var store = CreateStore();
        store.SetLastReportDate(date);

        // Load with a fresh instance to confirm persistence
        var store2 = CreateStore();
        store2.GetLastReportDate().Should().Be(date);
    }

    [Fact]
    public void SetLastReportDate_OverwritesPreviousDate()
    {
        var store = CreateStore();
        store.SetLastReportDate(new DateOnly(2025, 1, 1));
        store.SetLastReportDate(new DateOnly(2025, 6, 15));

        var store2 = CreateStore();
        store2.GetLastReportDate().Should().Be(new DateOnly(2025, 6, 15));
    }

    [Fact]
    public void GetLastReportDate_InvalidJson_ReturnsNull()
    {
        var stateFile = Path.Combine(_tempDir, "morning_report_state.json");
        File.WriteAllText(stateFile, "not valid json{{{");

        var store = CreateStore();
        store.GetLastReportDate().Should().BeNull();
    }

    [Fact]
    public void GetLastReportDate_ValidJsonMissingKey_ReturnsNull()
    {
        var stateFile = Path.Combine(_tempDir, "morning_report_state.json");
        File.WriteAllText(stateFile, "{\"otherKey\": \"2025-01-01\"}");

        var store = CreateStore();
        store.GetLastReportDate().Should().BeNull();
    }
}
