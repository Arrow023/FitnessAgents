using System.Collections.Concurrent;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models;

namespace FitnessAgentsWeb.Core.Services;

/// <summary>
/// In-memory tracker for background plan generation jobs.
/// Auto-cleans entries older than 1 hour.
/// </summary>
public class PlanGenerationTracker : IPlanGenerationTracker
{
    private readonly ConcurrentDictionary<string, PlanGenerationJob> _jobs = new();
    private DateTime _lastCleanup = DateTime.UtcNow;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan JobRetention = TimeSpan.FromHours(1);

    /// <inheritdoc />
    public string? Enqueue(string userId)
    {
        CleanupStaleJobs();

        // Reject if this user already has an in-progress job
        var existing = GetActiveJobForUser(userId);
        if (existing is not null)
        {
            return null;
        }

        var job = new PlanGenerationJob
        {
            JobId = Guid.NewGuid().ToString("N"),
            UserId = userId,
            Status = PlanGenerationStatus.Queued,
            CurrentStep = "Queued",
            QueuedAt = DateTime.UtcNow
        };

        _jobs[job.JobId] = job;
        return job.JobId;
    }

    /// <inheritdoc />
    public PlanGenerationJob? GetJob(string jobId) =>
        _jobs.TryGetValue(jobId, out var job) ? job : null;

    /// <inheritdoc />
    public PlanGenerationJob? GetActiveJobForUser(string userId) =>
        _jobs.Values.FirstOrDefault(j =>
            j.UserId == userId &&
            j.Status is not PlanGenerationStatus.Completed and not PlanGenerationStatus.Failed);

    /// <inheritdoc />
    public void UpdateStatus(string jobId, PlanGenerationStatus status, string step)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = status;
            job.CurrentStep = step;
        }
    }

    /// <inheritdoc />
    public void MarkCompleted(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = PlanGenerationStatus.Completed;
            job.CurrentStep = "Done";
            job.CompletedAt = DateTime.UtcNow;
        }
    }

    /// <inheritdoc />
    public void MarkFailed(string jobId, string error)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = PlanGenerationStatus.Failed;
            job.CurrentStep = "Failed";
            job.ErrorMessage = error;
            job.CompletedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Removes jobs older than the retention period to prevent memory leaks.
    /// </summary>
    private void CleanupStaleJobs()
    {
        if (DateTime.UtcNow - _lastCleanup < CleanupInterval) return;
        _lastCleanup = DateTime.UtcNow;

        var cutoff = DateTime.UtcNow - JobRetention;
        var staleKeys = _jobs
            .Where(kvp => kvp.Value.QueuedAt < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in staleKeys)
        {
            _jobs.TryRemove(key, out _);
        }
    }
}
