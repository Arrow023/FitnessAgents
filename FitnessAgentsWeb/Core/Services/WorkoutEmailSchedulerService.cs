using FitnessAgentsWeb.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Core.Services
{
    public class WorkoutEmailSchedulerService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;

        public WorkoutEmailSchedulerService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("[Scheduler] WorkoutEmailSchedulerService is starting.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndTriggerEmailsAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Scheduler Error] {ex.Message}");
                }

                // Check once per minute
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task CheckAndTriggerEmailsAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var storageRepo = scope.ServiceProvider.GetRequiredService<IStorageRepository>();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IAiOrchestratorService>();

            var profiles = await storageRepo.GetAllUserProfilesAsync();
            
            // Get current time in specified format "HH:mm"
            TimeZoneInfo istZone;
            try { istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
            catch { 
                try { istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
                catch { istZone = TimeZoneInfo.Local; } 
            }

            DateTime nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            string currentTimeString = nowIst.ToString("HH:mm");

            foreach (var userKvp in profiles)
            {
                var userId = userKvp.Key;
                var profile = userKvp.Value;

                if (!profile.IsActive) continue;

                // Example "08:00"
                if (profile.NotificationTime == currentTimeString)
                {
                    Console.WriteLine($"[Scheduler] Timing hit for {userId} ({currentTimeString}). Triggering AI Orchestration!");
                    
                    // We don't await the orchestrator directly so we don't block the loop for other users sharing this exact minute
                    _ = Task.Run(async () => 
                    {
                        try 
                        {
                            await orchestrator.ProcessAndGenerateAsync(userId);
                        }
                        catch (Exception ex) 
                        {
                            Console.WriteLine($"[Scheduler] Orchestration failed for {userId}: {ex.Message}");
                        }
                    });
                }
            }
        }
    }
}
