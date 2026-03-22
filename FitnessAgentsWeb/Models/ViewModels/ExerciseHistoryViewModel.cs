using FitnessAgentsWeb.Core.Helpers;

namespace FitnessAgentsWeb.Models.ViewModels;

public class ExerciseHistoryViewModel
{
    public required string UserId { get; set; }
    public List<ExerciseDayGroup> Days { get; set; } = new();
    public int TotalSessions { get; set; }
    public int TotalMinutes { get; set; }
}

public class ExerciseDayGroup
{
    public required string DateLabel { get; set; }
    public bool IsToday { get; set; }
    public List<ExerciseSessionItem> Sessions { get; set; } = new();
    public int TotalMinutes => Sessions.Sum(s => s.DurationMinutes);
}

public class ExerciseSessionItem
{
    public required string TypeCode { get; set; }
    public string Name => ExerciseTypeHelper.GetExerciseName(TypeCode);
    public string Icon => ExerciseTypeHelper.GetExerciseIcon(TypeCode);
    public int DurationMinutes { get; set; }
    public required string StartTime { get; set; }
    public required string EndTime { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
}

public class ExerciseDetailViewModel
{
    public required string UserId { get; set; }
    public required string Name { get; set; }
    public required string Icon { get; set; }
    public required string TypeCode { get; set; }
    public required string StartTime { get; set; }
    public required string EndTime { get; set; }
    public int DurationMinutes { get; set; }

    // Correlated metrics from overlapping Health Connect data
    public int? Steps { get; set; }
    public double? DistanceKm { get; set; }
    public double? ActiveCalories { get; set; }
    public double? TotalCalories { get; set; }
    public double? PaceMinPerKm { get; set; }

    // Heart rate during the session
    public int? AvgHeartRate { get; set; }
    public int? MinHeartRate { get; set; }
    public int? MaxHeartRate { get; set; }
    public List<HeartRatePoint> HeartRateTimeline { get; set; } = new();

    // Heart rate zones (% of duration)
    public List<HeartRateZone> HeartRateZones { get; set; } = new();

    // Workout score (estimated from HR zone distribution)
    public int? WorkoutScore { get; set; }
}

public class HeartRatePoint
{
    public required string Time { get; set; }
    public int Bpm { get; set; }
}

public class HeartRateZone
{
    public required string Label { get; set; }
    public required string Range { get; set; }
    public double Percentage { get; set; }
    public required string Color { get; set; }
}
