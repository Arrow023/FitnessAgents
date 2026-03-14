using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace FitnessAgentsWeb.Models;

public record HealthExportPayload
{
    [JsonPropertyName("sleep")] public List<SleepSession> Sleep { get; set; } = new();
    [JsonPropertyName("resting_heart_rate")] public List<HeartRateRecord> RestingHeartRate { get; set; } = new();
    [JsonPropertyName("steps")] public List<StepRecord> Steps { get; set; } = new();
    [JsonPropertyName("distance")] public List<DistanceRecord> Distance { get; set; } = new();
    [JsonPropertyName("active_calories")] public List<CalorieRecord> ActiveCalories { get; set; } = new();
    [JsonPropertyName("exercise")] public List<ExerciseRecord> Exercise { get; set; } = new();
}

public record SleepSession
{
    [JsonPropertyName("session_end_time")] public DateTime SessionEndTime { get; set; }
    [JsonPropertyName("duration_seconds")] public int DurationSeconds { get; set; }
    [JsonPropertyName("stages")] public List<SleepStage> Stages { get; set; } = new();
}
public record SleepStage
{
    [JsonPropertyName("stage")] public string Stage { get; set; }
    [JsonPropertyName("duration_seconds")] public int DurationSeconds { get; set; }
}

public record ExerciseRecord
{
    [JsonPropertyName("start_time")] 
    public DateTime StartTime { get; set; }
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("duration_seconds")] public int DurationSeconds { get; set; }
}

public record WeeklyWorkoutHistory
{
    [JsonPropertyName("week_start_date")] public DateTime WeekStartDate { get; set; }
    [JsonPropertyName("past_workouts")] public Dictionary<string, string> PastWorkouts { get; set; } = new();
}

public record HeartRateRecord { [JsonPropertyName("time")] public DateTime Time { get; set; } [JsonPropertyName("bpm")] public int Bpm { get; set; } }
public record StepRecord { [JsonPropertyName("end_time")] public DateTime EndTime { get; set; } [JsonPropertyName("count")] public int Count { get; set; } }
public record DistanceRecord { [JsonPropertyName("end_time")] public DateTime EndTime { get; set; } [JsonPropertyName("meters")] public double Meters { get; set; } }
public record CalorieRecord { [JsonPropertyName("end_time")] public DateTime EndTime { get; set; } [JsonPropertyName("calories")] public double Calories { get; set; } }
