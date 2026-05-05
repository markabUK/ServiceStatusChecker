using System;
using Jint;
using ServiceStatusChecker.Models;
using ServiceStatusChecker.Services;

namespace ServiceStatusChecker.Notifiers.Formatters;

public class JintMorningReportFormatter : IMorningReportFormatter
{
    private readonly string _script;
    public string Name { get; }

    public JintMorningReportFormatter(string name, string script)
    {
        Name = name;
        _script = script;
    }

    public string Format(MorningReportMessageContext context)
    {
        var engine = new Engine(options =>
        {
            options.LimitMemory(4_000_000);
            options.TimeoutInterval(TimeSpan.FromSeconds(2));
        });

        engine.SetValue("context", context);
        return engine.Evaluate(_script).AsString();
    }
}