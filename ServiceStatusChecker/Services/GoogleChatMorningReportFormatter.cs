using System.Text;
using ServiceStatusChecker.State;

namespace ServiceStatusChecker.Services;

public class GoogleChatMorningReportFormatter : IMorningReportFormatter
{
    public string Name => "google-chat";

    public string Format(MorningReportMessageContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Morning Service Status Report - {context.ReportDate:dddd, MMMM d, yyyy}");
        sb.AppendLine("Last status of monitored services from yesterday:");
        sb.AppendLine(new string('-', 50));

        foreach (var monitor in context.Monitors)
        {
            string icon = monitor.State switch
            {
                ServiceState.Up => "OK",
                ServiceState.Down => "DOWN",
                _ => "UNKNOWN"
            };

            sb.AppendLine($"{icon} {monitor.Name}: {monitor.State}");
            sb.AppendLine($"   Link: <{monitor.Url}|{monitor.Name} endpoint>");
        }

        sb.AppendLine(new string('-', 50));
        sb.AppendLine($"Generated at: {context.GeneratedAtUtc:u}");
        return sb.ToString();
    }
}