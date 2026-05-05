using System.Collections.Generic;
using System.Threading.Tasks;

namespace ServiceStatusChecker.Notifiers;

public interface INotifier<in TContext>
{
    IReadOnlyCollection<string> Handles { get; }
    string Name { get; }
    
    Task NotifyAsync(TContext context, string channel);
}