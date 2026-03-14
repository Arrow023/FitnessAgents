using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace FitnessAgentsWeb.Models
{
    public class DailyHealthMetrics
    {
        [JsonPropertyName("totalSleepMinutes")]
        public int TotalSleepMinutes { get; init; }

        [JsonPropertyName("averageRestingHeartRate")]
        public int AverageRestingHeartRate { get; init; }

        [JsonPropertyName("heartRateVariability")]
        public double HeartRateVariability { get; init; }
    }
}
