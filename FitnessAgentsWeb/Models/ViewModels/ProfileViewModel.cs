namespace FitnessAgentsWeb.Models.ViewModels;

public class ProfileViewModel
{
    public required string UserId { get; set; }
    public UserProfile? Profile { get; set; }
    public required string WebhookUrl { get; set; }
}
