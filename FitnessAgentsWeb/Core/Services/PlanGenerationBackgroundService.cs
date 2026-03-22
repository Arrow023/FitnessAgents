using System.Threading.Channels;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models;

namespace FitnessAgentsWeb.Core.Services;

/// <summary>
/// Background service that dequeues plan generation jobs from a channel
/// and executes them asynchronously, reporting progress via the tracker.
/// Pushes in-app notifications on start and completion.
/// </summary>
public class PlanGenerationBackgroundService : BackgroundService
{
    private readonly Channel<PlanGenerationJob> _channel;
    private readonly IPlanGenerationTracker _tracker;
    private readonly IAiOrchestratorService _orchestrator;
    private readonly IAppNotificationStore _notifications;
    private readonly ILogger<PlanGenerationBackgroundService> _logger;

    public PlanGenerationBackgroundService(
        Channel<PlanGenerationJob> channel,
        IPlanGenerationTracker tracker,
        IAiOrchestratorService orchestrator,
        IAppNotificationStore notifications,
        ILogger<PlanGenerationBackgroundService> logger)
    {
        _channel = channel;
        _tracker = tracker;
        _orchestrator = orchestrator;
        _notifications = notifications;
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

                _notifications.Push(new AppNotification
                {
                    UserId = job.UserId,
                    Title = "Plan Generation Started",
                    Message = "Your workout and diet plans are being generated...",
                    Type = NotificationType.PlanRequested,
                    Icon = "fa-solid fa-bolt",
                    Link = "/Workout"
                });

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

                    _notifications.Push(new AppNotification
                    {
                        UserId = job.UserId,
                        Title = "Workout Plan Ready",
                        Message = "Your AI-generated workout plan is ready to view.",
                        Type = NotificationType.WorkoutReady,
                        Icon = "fa-solid fa-dumbbell",
                        Link = "/Workout"
                    });

                    _notifications.Push(new AppNotification
                    {
                        UserId = job.UserId,
                        Title = "Diet Plan Ready",
                        Message = "Your personalized nutrition plan is ready to view.",
                        Type = NotificationType.DietReady,
                        Icon = "fa-solid fa-utensils",
                        Link = "/Diet"
                    });

                    _logger.LogInformation("[PlanGeneration] Job {JobId} completed successfully", job.JobId);
                }
                else
                {
                    _tracker.MarkFailed(job.JobId, "Plan generation returned false");

                    _notifications.Push(new AppNotification
                    {
                        UserId = job.UserId,
                        Title = "Generation Failed",
                        Message = "Plan generation could not complete. Please try again.",
                        Type = NotificationType.Error,
                        Icon = "fa-solid fa-circle-exclamation",
                        Link = "/Workout"
                    });

                    _logger.LogWarning("[PlanGeneration] Job {JobId} returned false", job.JobId);
                }
            }
            catch (Exception ex)
            {
                _tracker.MarkFailed(job.JobId, ex.Message);

                _notifications.Push(new AppNotification
                {
                    UserId = job.UserId,
                    Title = "Generation Error",
                    Message = "An unexpected error occurred during plan generation.",
                    Type = NotificationType.Error,
                    Icon = "fa-solid fa-circle-exclamation"
                });

                _logger.LogError(ex, "[PlanGeneration] Job {JobId} failed with exception", job.JobId);
            }
        }
    }
}
