namespace FitnessAgentsWeb.Models.ViewModels;

public class WorkoutDetailViewModel
{
    public required string UserId { get; set; }
    public required string DayOfWeek { get; set; }
    public required string RenderedHtml { get; set; }
}
