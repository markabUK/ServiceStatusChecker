using System;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ServiceStatusChecker.State;
using Xunit;

namespace ServiceStatusChecker.Tests.State;

public class JsonStateStoreTests : IDisposable
{
    private readonly string _tempFile;

    public JsonStateStoreTests()
    {
        _tempFile = Path.GetTempFileName();
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    [Fact]
    public void JsonStateStore_PersistsAndLoadsState()
    {
        // Arrange
        var options = Options.Create(new StateConfig { FilePath = _tempFile });
        
        // Write state with first instance
        var store1 = new JsonStateStore(options, NullLogger<JsonStateStore>.Instance);
        store1.Set("TestService", ServiceState.Down);
        store1.Get("TestService").Should().Be(ServiceState.Down);

        // Act: Load state with second instance reading the same file
        var store2 = new JsonStateStore(options, NullLogger<JsonStateStore>.Instance);
        
        // Assert
        store2.Get("TestService").Should().Be(ServiceState.Down);
        store2.Get("UnknownService").Should().Be(ServiceState.Unknown);
    }
}