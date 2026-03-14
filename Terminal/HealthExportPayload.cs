using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Terminal;

public class HealthExportPayload
{
    [JsonPropertyName("sleep")] public List<SleepSession> Sleep { get; init; } = new();
    [JsonPropertyName("resting_heart_rate")] public List<HeartRateRecord> RestingHeartRate { get; init; } = new();

    // Add these to catch your physical strain
    [JsonPropertyName("steps")] public List<StepRecord> Steps { get; init; } = new();
    [JsonPropertyName("exercise")] public List<ExerciseRecord> Exercise { get; init; } = new();
}

public record StepRecord
{
    [JsonPropertyName("end_time")] public DateTime EndTime { get; init; }
    [JsonPropertyName("count")] public int Count { get; init; }
}

public record ExerciseRecord
{
    [JsonPropertyName("start_time")] public DateTime StartTime { get; init; }
    [JsonPropertyName("type")] public string Type { get; init; }
    [JsonPropertyName("duration_seconds")] public int DurationSeconds { get; init; }
}

public record SleepSession
{
    [JsonPropertyName("session_end_time")]
    public DateTime SessionEndTime { get; init; }

    [JsonPropertyName("duration_seconds")]
    public int DurationSeconds { get; init; }
}

public record HeartRateRecord
{
    [JsonPropertyName("time")]
    public DateTime Time { get; init; }

    [JsonPropertyName("bpm")]
    public int Bpm { get; init; }
}
