using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace FitnessAgentsWeb.Core.Logging;

public class OtelLogEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("level", logEvent.Level.ToString()));

        var activity = Activity.Current;
        if (activity is not null)
        {
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("trace.id", activity.TraceId.ToString()));
            logEvent.AddOrUpdateProperty(propertyFactory.CreateProperty("span.id", activity.SpanId.ToString()));
        }
    }
}
