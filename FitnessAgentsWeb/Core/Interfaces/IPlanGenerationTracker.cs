using FitnessAgentsWeb.Models;

namespace FitnessAgentsWeb.Core.Interfaces;

/// <summary>
/// Tracks in-memory status of background plan generation jobs.
/// </summary>
public interface IPlanGenerationTracker
{
    /// <summary>
    /// Creates a new job entry for the given user and returns the job ID.
    /// Returns null if the user already has an active job.
    /// </summary>
    string? Enqueue(string userId);

    /// <summary>
    /// Retrieves a job by its unique ID.
    /// </summary>
    PlanGenerationJob? GetJob(string jobId);

    /// <summary>
    /// Retrieves the most recent active job for a user.
    /// </summary>
    PlanGenerationJob? GetActiveJobForUser(string userId);

    /// <summary>
    /// Updates the status and step description of a job.
    /// </summary>
    void UpdateStatus(string jobId, PlanGenerationStatus status, string step);

    /// <summary>
    /// Marks a job as completed.
    /// </summary>
    void MarkCompleted(string jobId);

    /// <summary>
    /// Marks a job as failed with an error message.
    /// </summary>
    void MarkFailed(string jobId, string error);
}
