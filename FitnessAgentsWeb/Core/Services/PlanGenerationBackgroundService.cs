using System.Threading.Channels;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models;

namespace FitnessAgentsWeb.Core.Services;

/// <summary>
/// Background service that dequeues plan generation jobs from a channel
/// and executes them asynchronously, reporting progress via the tracker.
/// </summary>
public class PlanGenerationBackgroundService : BackgroundService
{
    private readonly Channel<PlanGenerationJob> _channel;
    private readonly IPlanGenerationTracker _tracker;
    private readonly IAiOrchestratorService _orchestrator;
    private readonly ILogger<PlanGenerationBackgroundService> _logger;

    public PlanGenerationBackgroundService(
        Channel<PlanGenerationJob> channel,
        IPlanGenerationTracker tracker,
        IAiOrchestratorService orchestrator,
        ILogger<PlanGenerationBackgroundService> logger)
    {
        _channel = channel;
        _tracker = tracker;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[PlanGeneration] Background service started");

        await foreach (var job in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                _logger.LogInformation("[PlanGeneration] Processing job {JobId} for user {UserId}", job.JobId, job.UserId);

                // Create a progress reporter that updates the tracker
                var progress = new Progress<(PlanGenerationStatus Status, string Step)>(update =>
                {
                    _tracker.UpdateStatus(job.JobId, update.Status, update.Step);
                });

                var success = await _orchestrator.ProcessAndGenerateAsync(
                    job.UserId,
                    newPayload: null,
                    sendEmail: false,
                    progress: progress);

                if (success)
                {
                    _tracker.MarkCompleted(job.JobId);
                    _logger.LogInformation("[PlanGeneration] Job {JobId} completed successfully", job.JobId);
                }
                else
                {
                    _tracker.MarkFailed(job.JobId, "Plan generation returned false");
                    _logger.LogWarning("[PlanGeneration] Job {JobId} returned false", job.JobId);
                }
            }
            catch (Exception ex)
            {
                _tracker.MarkFailed(job.JobId, ex.Message);
                _logger.LogError(ex, "[PlanGeneration] Job {JobId} failed with exception", job.JobId);
            }
        }
    }
}
