using System.Collections.Concurrent;
using ServiceStatusChecker.State;

namespace ServiceStatusChecker.State;

public class MonitorStateStore
{
    private readonly ConcurrentDictionary<string, ServiceState> _states = new();

    public ServiceState Get(string name)
    {
        ServiceState state;
        if (_states.TryGetValue(name, out state))
        {
            return state;
        }

        return ServiceState.Unknown;
    }

    public void Set(string name, ServiceState state)
    {
        _states[name] = state;
    }
}