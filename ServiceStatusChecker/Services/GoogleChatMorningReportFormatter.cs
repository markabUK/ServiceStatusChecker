using System;
using System.Text;

namespace ServiceStatusChecker.Services;

public class GoogleChatMorningReportFormatter : IMorningReportFormatter
{
    public string Name => "google-chat";

    public string Format(string baseMessage)
    {
        var lines = baseMessage.Split(Environment.NewLine);
        var sb = new StringBuilder();

        foreach (var line in lines)
        {
            const string prefix = "   URL: ";
            if (line.StartsWith(prefix, StringComparison.Ordinal))
            {
                string url = line.Substring(prefix.Length).Trim();
                if (!string.IsNullOrWhiteSpace(url))
                {
                    sb.AppendLine($"   Link: <{url}|Open endpoint>");
                    continue;
                }
            }

            sb.AppendLine(line);
        }

        return sb.ToString();
    }
}
