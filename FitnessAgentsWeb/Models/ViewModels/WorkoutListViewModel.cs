namespace FitnessAgentsWeb.Models.ViewModels;

public class WorkoutListViewModel
{
    public required string UserId { get; set; }
    public WeeklyWorkoutHistory? WeeklyHistory { get; set; }
    public Dictionary<string, string> RenderedHtml { get; set; } = new();
}
