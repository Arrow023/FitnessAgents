using FitnessAgentsWeb.Core.Factories;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models;
using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Core.Services
{
    public class AiOrchestratorService : IAiOrchestratorService
    {
        private readonly StorageRepositoryFactory _storageFactory;
        private readonly HealthDataProcessorFactory _processorFactory;
        private readonly AiAgentServiceFactory _aiFactory;
        private readonly NotificationServiceFactory _notificationFactory;
        private readonly DieticianAgentService _dieticianService;

        public AiOrchestratorService(
            StorageRepositoryFactory storageFactory,
            HealthDataProcessorFactory processorFactory,
            AiAgentServiceFactory aiFactory,
            NotificationServiceFactory notificationFactory,
            DieticianAgentService dieticianService)
        {
            _storageFactory = storageFactory;
            _processorFactory = processorFactory;
            _aiFactory = aiFactory;
            _notificationFactory = notificationFactory;
            _dieticianService = dieticianService;
        }

        public async Task<bool> AppendHealthDataAsync(string userId, HealthExportPayload newPayload)
        {
            try
            {
                var storageRepo = _storageFactory.Create();
                var healthProcessor = _processorFactory.Create();

                var finalPayloadToProcess = await healthProcessor.ProcessAndMergeHealthDataAsync(userId, newPayload);

                // Save merged data back to Firebase
                string mergedJson = JsonSerializer.Serialize(finalPayloadToProcess, new JsonSerializerOptions { WriteIndented = true });
                await storageRepo.SaveTodayHealthDataAsync(userId, mergedJson);
                Console.WriteLine($"[System] Health data smartly merged and saved for {userId}.");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error in AppendHealthDataAsync] {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ProcessAndGenerateAsync(string userId, HealthExportPayload? newPayload = null)
        {
            try
            {
                var storageRepo = _storageFactory.Create();
                var healthProcessor = _processorFactory.Create();
                var aiAgent = _aiFactory.Create();
                var notifier = _notificationFactory.Create();

                HealthExportPayload? finalPayloadToProcess = newPayload;

                if (newPayload != null)
                {
                    finalPayloadToProcess = await healthProcessor.ProcessAndMergeHealthDataAsync(userId, newPayload);
                    string mergedJson = JsonSerializer.Serialize(finalPayloadToProcess, new JsonSerializerOptions { WriteIndented = true });
                    await storageRepo.SaveTodayHealthDataAsync(userId, mergedJson);
                }
                else
                {
                    // CRITICAL FIX: If no new payload, fetch existing today's data from storage
                    // Otherwise userContext remains empty during manual triggers/scheduled runs
                    finalPayloadToProcess = await storageRepo.GetTodayHealthDataAsync(userId);
                }

                Console.WriteLine($"[System] Booting AI in background for {userId}...");

                // LOAD ALL DATA INTO MEMORY ONCE
                var userContext = await healthProcessor.LoadHealthStateToRAMAsync(userId, finalPayloadToProcess);

                // Run the AI Agent (Workout)
                string workoutMarkdown = await aiAgent.GenerateWorkoutAsync(userContext);

                // Run the AI Dietician (Nutrition - JSON)
                string dietJsonString = await _dieticianService.GenerateRecoveryDietJsonAsync(workoutMarkdown, userContext);
                
                DietPlan? dietPlan = null;
                string dietMarkdown = "No diet generated.";

                if (!string.IsNullOrEmpty(dietJsonString) && dietJsonString != "{}")
                {
                    try
                    {
                        dietPlan = JsonSerializer.Deserialize<DietPlan>(dietJsonString);
                        if (dietPlan != null)
                        {
                            await storageRepo.SaveLatestDietAsync(userId, dietPlan);

                            // Format back to markdown for the email
                            var sb = new System.Text.StringBuilder();
                            sb.AppendLine($"# Diet Recommended: {dietPlan.TotalCaloriesTarget} kcal limit");
                            sb.AppendLine($"*{dietPlan.AiSummary}*\n");
                            foreach (var meal in dietPlan.Meals)
                            {
                                sb.AppendLine($"- **{meal.MealType}**: {meal.FoodName} ({meal.QuantityDescription}) - {meal.Calories} kcal");
                            }
                            dietMarkdown = sb.ToString();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[AiOrchestrator] JSON Parse Failed for Diet: {ex.Message}");
                    }
                }

                string finalMarkdown = workoutMarkdown + "\n\n---\n\n" + dietMarkdown;

                // Save to History
                var weeklyHistory = await storageRepo.GetWeeklyHistoryAsync(userId) ?? new WeeklyWorkoutHistory();

                DateTime nowIst;
                try { nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")); }
                catch { nowIst = DateTime.Now; }

                string todayString = nowIst.DayOfWeek.ToString();

                weeklyHistory.PastWorkouts[todayString] = finalMarkdown;
                await storageRepo.SaveWeeklyHistoryAsync(userId, weeklyHistory);

                // Send the Email
                if (!string.IsNullOrEmpty(userContext.Email))
                {
                    await notifier.SendWorkoutNotificationAsync(userContext.Email, finalMarkdown, userContext);
                }
                else
                {
                    Console.WriteLine($"[System] No email configured for {userId}. Skipping email notification.");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Error in AiOrchestratorService] {ex.Message}");
                return false;
            }
        }
    }
}
