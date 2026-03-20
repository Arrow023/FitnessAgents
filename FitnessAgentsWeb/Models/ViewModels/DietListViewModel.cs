namespace FitnessAgentsWeb.Models.ViewModels;

public class DietListViewModel
{
    public required string UserId { get; set; }
    public DietPlan? LatestDiet { get; set; }
    public WeeklyDietHistory? DietHistory { get; set; }
}
