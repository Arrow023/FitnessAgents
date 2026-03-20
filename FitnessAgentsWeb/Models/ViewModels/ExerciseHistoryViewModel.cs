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
}
