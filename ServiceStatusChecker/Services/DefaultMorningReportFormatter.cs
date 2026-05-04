using System.Text;
using ServiceStatusChecker.State;

namespace ServiceStatusChecker.Services;

public class DefaultMorningReportFormatter : IMorningReportFormatter
{
    public string Name => "default";

    public string Format(MorningReportMessageContext context)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"📋 Morning Service Status Report — {context.ReportDate:dddd, MMMM d, yyyy}");
        sb.AppendLine("Last status of monitored services from yesterday: ");
        sb.AppendLine(new string('─', 50));

        foreach (var monitor in context.Monitors)
        {
            string icon = monitor.State switch
            {
                ServiceState.Up => "✅",
                ServiceState.Down => "❌",
                _ => "❓"
            };

            sb.AppendLine($"{icon} {monitor.Name}: {monitor.State}");
            sb.AppendLine($"   URL: {monitor.Url}");
        }

        sb.AppendLine(new string('─', 50));
        sb.AppendLine($"Generated at: {context.GeneratedAtUtc:u}");

        return sb.ToString();
    }
}