using Serilog.Core;
using Serilog.Events;
using FitnessAgentsWeb.Core.Helpers;

namespace FitnessAgentsWeb.Core.Logging
{
    public class TimezoneTimestampEnricher : ILogEventEnricher
    {
        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            var appNow = TimezoneHelper.GetAppNow();
            var property = propertyFactory.CreateProperty("AppTimestamp", appNow.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            logEvent.AddOrUpdateProperty(property);
        }
    }
}
