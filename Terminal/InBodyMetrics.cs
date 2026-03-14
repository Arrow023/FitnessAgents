using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Terminal
{
    public class InBodyMetrics
    {
        [JsonPropertyName("skeletalMuscleMassKg")]
        public double SkeletalMuscleMassKg { get; init; }

        [JsonPropertyName("percentBodyFat")]
        public double PercentBodyFat { get; init; }

        [JsonPropertyName("basalMetabolicRate")]
        public int BasalMetabolicRate { get; init; }
    }
}
