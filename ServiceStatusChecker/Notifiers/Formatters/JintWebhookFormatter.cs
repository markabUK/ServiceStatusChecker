using System;
using Jint;
using ServiceStatusChecker.Models;

namespace ServiceStatusChecker.Notifiers.Formatters;

public class JintWebhookFormatter : IWebhookBodyFormatter
{
    private readonly string _script;
    public string Name { get; }

    public JintWebhookFormatter(string name, string script)
    {
        Name = name;
        _script = script;
    }

    public string Format(NotificationContext context)
    {
        // Locked down Engine: restricts memory and execution time
        var engine = new Engine(options =>
        {
            options.LimitMemory(4_000_000); 
            options.TimeoutInterval(TimeSpan.FromSeconds(2));
        });

        engine.SetValue("context", context);

        // Execute the script. The script should implicitly evaluate to the formatted string.
        return engine.Evaluate(_script).AsString();
    }
}