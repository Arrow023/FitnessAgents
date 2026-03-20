using FitnessAgentsWeb.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Core.Services
{
    public class WorkoutEmailSchedulerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Microsoft.Extensions.Logging.ILogger<WorkoutEmailSchedulerService> _logger;

        // Tracks the last date each user was triggered to prevent duplicates and ensure exactly-once per day
        private readonly ConcurrentDictionary<string, DateOnly> _lastTriggeredDate = new();

        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

        public WorkoutEmailSchedulerService(IServiceProvider serviceProvider, Microsoft.Extensions.Logging.ILogger<WorkoutEmailSchedulerService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[Scheduler] WorkoutEmailSchedulerService is starting.");

            using var timer = new PeriodicTimer(CheckInterval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await CheckAndTriggerEmailsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[Scheduler] Unexpected error during check cycle");
                }
            }
        }

        private async Task CheckAndTriggerEmailsAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var storageRepo = scope.ServiceProvider.GetRequiredService<IStorageRepository>();

            var profiles = await storageRepo.GetAllUserProfilesAsync();

            var istZone = GetIstTimeZone();
            var nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            var todayIst = DateOnly.FromDateTime(nowIst);
            var currentTime = TimeOnly.FromDateTime(nowIst);

            // Clean up stale entries from previous days
            foreach (var entry in _lastTriggeredDate)
            {
                if (entry.Value < todayIst)
                    _lastTriggeredDate.TryRemove(entry.Key, out _);
            }

            foreach (var userKvp in profiles)
            {
                var userId = userKvp.Key;
                var profile = userKvp.Value;

                if (!profile.IsActive) continue;

                if (!TimeOnly.TryParseExact(profile.NotificationTime, "HH:mm", out var scheduledTime))
                {
                    _logger.LogWarning("[Scheduler] Invalid NotificationTime '{Time}' for user {UserId}", profile.NotificationTime, userId);
                    continue;
                }

                // Trigger if current time is within 30 minutes past the scheduled time AND we haven't triggered today
                // This prevents re-triggering stale schedules after an app restart (e.g., User alpha 08:00 firing at 16:00)
                var minutesPastSchedule = (currentTime - scheduledTime).TotalMinutes;
                if (minutesPastSchedule >= 0 && minutesPastSchedule <= 30 && !_lastTriggeredDate.ContainsKey(userId))
                {
                    if (!_lastTriggeredDate.TryAdd(userId, todayIst))
                        continue; // Another thread already added it

                    _logger.LogInformation("[Scheduler] Triggering AI Orchestration for {UserId} (scheduled: {Scheduled}, current: {Current})",
                        userId, profile.NotificationTime, currentTime.ToString("HH:mm"));

                    // Create a dedicated scope for the background work so it isn't disposed prematurely
                    var taskScope = _serviceProvider.CreateScope();
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var orchestrator = taskScope.ServiceProvider.GetRequiredService<IAiOrchestratorService>();
                            await orchestrator.ProcessAndGenerateAsync(userId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "[Scheduler] Orchestration failed for {UserId}", userId);
                            // Remove the tracking entry so it can be retried on the next cycle
                            _lastTriggeredDate.TryRemove(userId, out _);
                        }
                        finally
                        {
                            taskScope.Dispose();
                        }
                    }, stoppingToken);
                }
            }
        }

        private static TimeZoneInfo GetIstTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
            catch
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
                catch { return TimeZoneInfo.Local; }
            }
        }
    }
}
