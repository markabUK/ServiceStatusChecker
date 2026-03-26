using System.Text.Json.Serialization;

namespace ServiceStatusChecker.State;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class JsonStateStore
{
    private readonly string _filePath;
    private readonly ILogger<JsonStateStore> _logger;
    private readonly ConcurrentDictionary<string, ServiceState> _states;
    private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

    public JsonStateStore(IOptions<StateConfig> options, ILogger<JsonStateStore> logger)
    {
        _filePath = options.Value.FilePath;
        _logger = logger;
        _states = new ConcurrentDictionary<string, ServiceState>(LoadFromFile());
    }

    public ServiceState Get(string name)
    {
        if (_states.TryGetValue(name, out ServiceState state))
        {
            return state;
        }

        return ServiceState.Unknown;
    }

    public void Set(string name, ServiceState state)
    {
        _states[name] = state;
        SaveToFile();
    }

    private IDictionary<string, ServiceState> LoadFromFile()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return new Dictionary<string, ServiceState>();
            }

            string json = File.ReadAllText(_filePath);
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());

            Dictionary<string, ServiceState>? data =
                JsonSerializer.Deserialize<Dictionary<string, ServiceState>>(json, options);

            return data ?? new Dictionary<string, ServiceState>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load state file {FilePath}", _filePath);
            return new Dictionary<string, ServiceState>();
        }
    }

    private void SaveToFile()
    {
        _lock.Wait();
        try
        {
            Dictionary<string, ServiceState> snapshot = new Dictionary<string, ServiceState>(_states);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            options.Converters.Add(new JsonStringEnumConverter());

            string json = JsonSerializer.Serialize(snapshot, options);
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save state file {FilePath}", _filePath);
        }
        finally
        {
            _lock.Release();
        }
    }
}