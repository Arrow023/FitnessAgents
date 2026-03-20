namespace FitnessAgentsWeb.Models.ViewModels;

public class OverviewViewModel
{
    public required string UserId { get; set; }
    public required UserHealthContext Context { get; set; }
    public InBodyExport? InBody { get; set; }
    public HealthExportPayload? HealthData { get; set; }
}
