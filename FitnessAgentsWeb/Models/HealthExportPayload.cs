using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace FitnessAgentsWeb.Models;

public record HealthExportPayload
{
    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; set; }

    [JsonPropertyName("app_version")]
    public string? AppVersion { get; set; }

    [JsonPropertyName("steps")]
    public List<StepRecord> Steps { get; set; } = new();

    [JsonPropertyName("sleep")]
    public List<SleepSession> Sleep { get; set; } = new();

    [JsonPropertyName("heart_rate")]
    public List<HeartRateRecord> HeartRate { get; set; } = new();

    [JsonPropertyName("resting_heart_rate")]
    public List<HeartRateRecord> RestingHeartRate { get; set; } = new();

    [JsonPropertyName("heart_rate_variability")]
    public List<HrvRecord> HRV { get; set; } = new();

    [JsonPropertyName("active_calories")]
    public List<CalorieRecord> ActiveCalories { get; set; } = new();

    [JsonPropertyName("total_calories")]
    public List<CalorieRecord> TotalCalories { get; set; } = new();

    [JsonPropertyName("distance")]
    public List<DistanceRecord> Distance { get; set; } = new();

    [JsonPropertyName("exercise")]
    public List<ExerciseRecord> Exercise { get; set; } = new();

    [JsonPropertyName("weight")]
    public List<WeightRecord> Weight { get; set; } = new();

    [JsonPropertyName("height")]
    public List<HeightRecord> Height { get; set; } = new();

    [JsonPropertyName("blood_pressure")]
    public List<BloodPressureRecord> BloodPressure { get; set; } = new();

    [JsonPropertyName("blood_glucose")]
    public List<BloodGlucoseRecord> BloodGlucose { get; set; } = new();

    [JsonPropertyName("oxygen_saturation")]
    public List<OxygenSaturationRecord> OxygenSaturation { get; set; } = new();

    [JsonPropertyName("body_temperature")]
    public List<BodyTemperatureRecord> BodyTemperature { get; set; } = new();

    [JsonPropertyName("respiratory_rate")]
    public List<RespiratoryRateRecord> RespiratoryRate { get; set; } = new();

    [JsonPropertyName("hydration")]
    public List<HydrationRecord> Hydration { get; set; } = new();

    [JsonPropertyName("nutrition")]
    public List<NutritionRecord> Nutrition { get; set; } = new();

    [JsonPropertyName("vo2max")]
    public List<Vo2MaxRecord> Vo2Max { get; set; } = new();
}

public record StepRecord
{
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("start_time")] public DateTime StartTime { get; set; }
    [JsonPropertyName("end_time")] public DateTime EndTime { get; set; }
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
    [JsonPropertyName("start_time")] public DateTime StartTime { get; set; }
    [JsonPropertyName("end_time")] public DateTime EndTime { get; set; }
}

public record HeartRateRecord
{
    [JsonPropertyName("bpm")] public int Bpm { get; set; }
    [JsonPropertyName("time")] public DateTime Time { get; set; }
}

public record HrvRecord
{
    [JsonPropertyName("rmssd_millis")] public double Rmssd { get; set; }
    [JsonPropertyName("time")] public DateTime Time { get; set; }
}

public record CalorieRecord
{
    [JsonPropertyName("calories")] public double Calories { get; set; }
    [JsonPropertyName("start_time")] public DateTime StartTime { get; set; }
    [JsonPropertyName("end_time")] public DateTime EndTime { get; set; }
}

public record DistanceRecord
{
    [JsonPropertyName("meters")] public double Meters { get; set; }
    [JsonPropertyName("start_time")] public DateTime StartTime { get; set; }
    [JsonPropertyName("end_time")] public DateTime EndTime { get; set; }
}

public record ExerciseRecord
{
    [JsonPropertyName("type")] public string Type { get; set; }
    [JsonPropertyName("start_time")] public DateTime StartTime { get; set; }
    [JsonPropertyName("end_time")] public DateTime EndTime { get; set; }
    [JsonPropertyName("duration_seconds")] public int DurationSeconds { get; set; }
}

public record WeightRecord
{
    [JsonPropertyName("kilograms")] public double Kilograms { get; set; }
    [JsonPropertyName("time")] public DateTime Time { get; set; }
}

public record HeightRecord
{
    [JsonPropertyName("meters")] public double Meters { get; set; }
    [JsonPropertyName("time")] public DateTime Time { get; set; }
}

public record BloodPressureRecord
{
    [JsonPropertyName("systolic")] public double Systolic { get; set; }
    [JsonPropertyName("diastolic")] public double Diastolic { get; set; }
    [JsonPropertyName("time")] public DateTime Time { get; set; }
}

public record BloodGlucoseRecord
{
    [JsonPropertyName("mmol_per_liter")] public double MmolPerLiter { get; set; }
    [JsonPropertyName("time")] public DateTime Time { get; set; }
}

public record OxygenSaturationRecord
{
    [JsonPropertyName("percentage")] public double Percentage { get; set; }
    [JsonPropertyName("time")] public DateTime Time { get; set; }
}

public record BodyTemperatureRecord
{
    [JsonPropertyName("celsius")] public double Celsius { get; set; }
    [JsonPropertyName("time")] public DateTime Time { get; set; }
}

public record RespiratoryRateRecord
{
    [JsonPropertyName("rate")] public double Rate { get; set; }
    [JsonPropertyName("time")] public DateTime Time { get; set; }
}

public record HydrationRecord
{
    [JsonPropertyName("liters")] public double Liters { get; set; }
    [JsonPropertyName("start_time")] public DateTime StartTime { get; set; }
    [JsonPropertyName("end_time")] public DateTime EndTime { get; set; }
}

public record NutritionRecord
{
    [JsonPropertyName("calories")] public double? Calories { get; set; }
    [JsonPropertyName("protein_grams")] public double? ProteinGrams { get; set; }
    [JsonPropertyName("carbs_grams")] public double? CarbsGrams { get; set; }
    [JsonPropertyName("fat_grams")] public double? FatGrams { get; set; }
    [JsonPropertyName("start_time")] public DateTime StartTime { get; set; }
    [JsonPropertyName("end_time")] public DateTime EndTime { get; set; }
}

public record Vo2MaxRecord
{
    [JsonPropertyName("vo2_ml_per_min_kg")] public double Vo2MlPerMinKg { get; set; }
    [JsonPropertyName("time")] public DateTime Time { get; set; }
}

public record WeeklyWorkoutHistory
{
    [JsonPropertyName("week_start_date")] public DateTime WeekStartDate { get; set; }
    [JsonPropertyName("past_workouts")] public Dictionary<string, string> PastWorkouts { get; set; } = new();
    [JsonPropertyName("past_workout_plans")] public Dictionary<string, WorkoutPlan> PastWorkoutPlans { get; set; } = new();
}

public record WeeklyDietHistory
{
    [JsonPropertyName("week_start_date")] public DateTime WeekStartDate { get; set; }
    [JsonPropertyName("past_diets")] public Dictionary<string, DietPlan> PastDiets { get; set; } = new();
}