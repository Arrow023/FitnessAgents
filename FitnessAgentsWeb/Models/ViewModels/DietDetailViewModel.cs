namespace FitnessAgentsWeb.Models.ViewModels;

public class DietDetailViewModel
{
    public required string UserId { get; set; }
    public required string DayOfWeek { get; set; }
    public required DietPlan Diet { get; set; }
}
