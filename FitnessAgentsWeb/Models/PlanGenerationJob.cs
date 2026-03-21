namespace FitnessAgentsWeb.Models;

/// <summary>
/// Tracks the status of an asynchronous plan generation job.
/// </summary>
public class PlanGenerationJob
{
    public required string JobId { get; set; }
    public required string UserId { get; set; }
    public PlanGenerationStatus Status { get; set; } = PlanGenerationStatus.Queued;
    public string CurrentStep { get; set; } = "Queued";
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Represents the discrete stages of the plan generation pipeline.
/// </summary>
public enum PlanGenerationStatus
{
    Queued,
    LoadingHealthData,
    QueryingHistory,
    GeneratingWorkout,
    GeneratingDiet,
    ValidatingDiet,
    SavingPlans,
    SendingNotification,
    Completed,
    Failed
}
