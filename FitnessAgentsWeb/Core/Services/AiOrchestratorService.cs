using FitnessAgentsWeb.Core.Factories;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Core.Configuration;
using FitnessAgentsWeb.Models;
using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;
using FitnessAgentsWeb.Core.Helpers;

namespace FitnessAgentsWeb.Core.Services
{
    public class AiOrchestratorService : IAiOrchestratorService
    {
        private readonly StorageRepositoryFactory _storageFactory;
        private readonly HealthDataProcessorFactory _processorFactory;
        private readonly AiAgentServiceFactory _aiFactory;
        private readonly NotificationServiceFactory _notificationFactory;
        private readonly IAppConfigurationProvider _configProvider;
        private readonly Microsoft.Extensions.Logging.ILogger<AiOrchestratorService> _logger;

        public AiOrchestratorService(
            StorageRepositoryFactory storageFactory,
            HealthDataProcessorFactory processorFactory,
            AiAgentServiceFactory aiFactory,
            NotificationServiceFactory notificationFactory,
            IAppConfigurationProvider configProvider,
            Microsoft.Extensions.Logging.ILogger<AiOrchestratorService> logger)
        {
            _storageFactory = storageFactory;
            _processorFactory = processorFactory;
            _aiFactory = aiFactory;
            _notificationFactory = notificationFactory;
            _configProvider = configProvider;
            _logger = logger;
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
                _logger.LogInformation($"[System] Health data smartly merged and saved for {userId}.");

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[Error in AppendHealthDataAsync] for {userId}");
                return false;
            }
        }

        public async Task<bool> ProcessAndGenerateAsync(string userId, HealthExportPayload? newPayload = null, bool sendEmail = true)
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

                _logger.LogInformation($"[System] Booting AI in background for {userId}...");

                // LOAD ALL DATA INTO MEMORY ONCE
                var userContext = await healthProcessor.LoadHealthStateToRAMAsync(userId, finalPayloadToProcess);

                // Run the AI Agent (Workout)
                string workoutJsonString = await aiAgent.GenerateWorkoutAsync(userContext);

                // Run the AI Dietician (Nutrition - JSON)
                string dietJsonString = await aiAgent.GenerateRecoveryDietJsonAsync(workoutJsonString, userContext);
                
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
                        _logger.LogError(ex, "[AiOrchestrator] JSON Parse Failed for Diet");
                    }
                }

                // Format Workout JSON to Markdown
                string workoutMarkdown = "";
                if (!string.IsNullOrEmpty(workoutJsonString) && workoutJsonString != "{}")
                {
                    try
                    {
                        var workoutData = JsonDocument.Parse(workoutJsonString).RootElement;
                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine($"# {workoutData.GetProperty("session_title").GetString()}");
                        sb.AppendLine($"<p style='font-size: 0.85rem; color: #6b7280; margin-top: -10px;'>Date: {workoutData.GetProperty("plan_date").GetString()}</p>\n");

                        if (workoutData.TryGetProperty("personalized_introduction", out var intro))
                        {
                            sb.AppendLine($"{intro.GetString()}\n");
                        }
                        
                        sb.AppendLine("## Warmup");
                        foreach (var item in workoutData.GetProperty("warmup").EnumerateArray())
                        {
                            sb.AppendLine($"- **{item.GetProperty("exercise").GetString()}**: {item.GetProperty("instruction").GetString()}");
                        }

                        sb.AppendLine("\n## Main Workout");
                        sb.AppendLine("| Exercise | Sets | Reps | Rest | Notes |");
                        sb.AppendLine("| :--- | :--- | :--- | :--- | :--- |");
                        foreach (var item in workoutData.GetProperty("main_workout").EnumerateArray())
                        {
                            sb.AppendLine($"| {item.GetProperty("exercise").GetString()} | {item.GetProperty("sets")} | {item.GetProperty("reps").GetString()} | {item.GetProperty("rest").GetString()} | {item.GetProperty("notes").GetString()} |");
                        }

                        sb.AppendLine("\n## Cooldown");
                        foreach (var item in workoutData.GetProperty("cooldown").EnumerateArray())
                        {
                            sb.AppendLine($"- **{item.GetProperty("exercise").GetString()}** ({item.GetProperty("duration").GetString()})");
                        }

                        sb.AppendLine($"\n---");
                        sb.AppendLine($"**Coach Notes**: {workoutData.GetProperty("coach_notes").GetString()}");
                        sb.AppendLine("\nStay strong, <br><br>**Apex** <br>*Your AI Biomechanics Specialist*");

                        workoutMarkdown = sb.ToString();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[AiOrchestrator] Workout JSON Formatting Failed");
                        workoutMarkdown = workoutJsonString; // Fallback
                    }
                }

                string finalMarkdown = workoutMarkdown + "\n\n---\n\n" + dietMarkdown;

                // Save to History (Workout & Diet)
                var weeklyHistory = await storageRepo.GetWeeklyHistoryAsync(userId) ?? new WeeklyWorkoutHistory();
                var weeklyDietHistory = await storageRepo.GetWeeklyDietHistoryAsync(userId) ?? new WeeklyDietHistory();

                string tzId = _configProvider.GetAppTimezone();
                var appNow = TimezoneHelper.GetAppNow(tzId);
                string todayString = appNow.DayOfWeek.ToString();

                weeklyHistory.PastWorkouts[todayString] = finalMarkdown;
                await storageRepo.SaveWeeklyHistoryAsync(userId, weeklyHistory);

                if (dietPlan != null)
                {
                    weeklyDietHistory.PastDiets[todayString] = dietPlan;
                    await storageRepo.SaveWeeklyDietHistoryAsync(userId, weeklyDietHistory);
                }

                // Send the Email (Combined)
                if (sendEmail)
                {
                    if (!string.IsNullOrEmpty(userContext.Email))
                    {
                        await notifier.SendWorkoutNotificationAsync(userContext.Email, finalMarkdown, userContext);
                    }
                    else
                    {
                        _logger.LogWarning($"[System] No email configured for {userId}. Skipping email notification.");
                    }
                }
                else
                {
                    _logger.LogInformation($"[System] Generation only for {userId}. Skipping automated email.");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[Error in AiOrchestratorService] processing for {userId}");
                return false;
            }
        }

        public async Task<bool> EmailStoreDietPlanAsync(string userId, DietPlan diet)
        {
            try
            {
                var healthProcessor = _processorFactory.Create();
                var notifier = _notificationFactory.Create();

                var healthData = await _storageFactory.Create().GetTodayHealthDataAsync(userId);
                var userContext = await healthProcessor.LoadHealthStateToRAMAsync(userId, healthData);

                if (string.IsNullOrEmpty(userContext.Email))
                {
                    _logger.LogWarning($"[System] No email for {userId}. Cannot send Diet plan.");
                    return false;
                }

                await notifier.SendDietNotificationAsync(userContext.Email, diet, userContext);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[Error] Failed to email stored diet plan for {userId}");
                return false;
            }
        }
    }
}
