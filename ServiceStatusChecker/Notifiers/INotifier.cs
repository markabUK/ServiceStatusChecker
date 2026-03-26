using System.Collections.Generic;
using System.Threading.Tasks;
using ServiceStatusChecker.Models;

namespace ServiceStatusChecker.Notifiers;

public interface INotifier
{


    IReadOnlyCollection<string> Handles { get; }
    Task NotifyAsync(NotificationContext context, string channel);
    string Name { get; }
}
