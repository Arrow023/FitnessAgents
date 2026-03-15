using System;
using System.Collections.Generic;
using System.Linq;

namespace FitnessAgentsWeb.Core.Helpers
{
    public static class TimezoneHelper
    {
        public static string CurrentTimezoneId { get; set; } = "India Standard Time";

        public static List<string> GetAllTimezones()
        {
            return TimeZoneInfo.GetSystemTimeZones()
                .Select(tz => tz.Id)
                .OrderBy(id => id)
                .ToList();
        }

        public static DateTime GetAppNow(string timezoneId)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
                return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            }
            catch
            {
                // Fallback to UTC if timezone ID is invalid
                return DateTime.UtcNow;
            }
        }

        public static DateTime GetAppNow()
        {
            return GetAppNow(CurrentTimezoneId);
        }

        public static DateTime ConvertToAppTime(DateTime utcTime, string timezoneId)
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
                return TimeZoneInfo.ConvertTimeFromUtc(utcTime, tz);
            }
            catch
            {
                return utcTime;
            }
        }
    }
}
