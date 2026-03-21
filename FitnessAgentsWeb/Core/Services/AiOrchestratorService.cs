using FitnessAgentsWeb.Core.Factories;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Core.Configuration;
using FitnessAgentsWeb.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private readonly IPlanVectorStore? _vectorStore;
        private readonly IEmbeddingService? _embeddingService;
        private readonly Microsoft.Extensions.Logging.ILogger<AiOrchestratorService> _logger;

        public AiOrchestratorService(
            StorageRepositoryFactory storageFactory,
            HealthDataProcessorFactory processorFactory,
            AiAgentServiceFactory aiFactory,
            NotificationServiceFactory notificationFactory,
            IAppConfigurationProvider configProvider,
            Microsoft.Extensions.Logging.ILogger<AiOrchestratorService> logger,
            IPlanVectorStore? vectorStore = null,
            IEmbeddingService? embeddingService = null)
        {
            _storageFactory = storageFactory;
            _processorFactory = processorFactory;
            _aiFactory = aiFactory;
            _notificationFactory = notificationFactory;
            _configProvider = configProvider;
            _logger = logger;
            _vectorStore = vectorStore;
            _embeddingService = embeddingService;
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

        public async Task<bool> ProcessAndGenerateAsync(string userId, HealthExportPayload? newPayload = null, bool sendEmail = true, IProgress<(PlanGenerationStatus Status, string Step)>? progress = null)
        {
            try
            {
                var storageRepo = _storageFactory.Create();
                var healthProcessor = _processorFactory.Create();
                var aiAgent = _aiFactory.Create();
                var notifier = _notificationFactory.Create();

                progress?.Report((PlanGenerationStatus.LoadingHealthData, "Loading health data..."));

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

                string tzId = _configProvider.GetAppTimezone();
                var appNow = TimezoneHelper.GetAppNow(tzId);
                string todayString = appNow.DayOfWeek.ToString();

                // Query vector store for similar historical plans + recent feedback
                string workoutSimilarContext = "";
                string workoutFeedbackContext = "";
                string dietSimilarContext = "";
                string dietFeedbackContext = "";
                string digestContext = "";

                progress?.Report((PlanGenerationStatus.QueryingHistory, "Querying historical plans & feedback..."));

                if (_vectorStore is not null && _embeddingService is not null)
                {
                    try
                    {
                        string todayDay = appNow.ToString("dddd");
                        string todayScheduledFocus = userContext.WorkoutSchedule.TryGetValue(todayDay, out var focus) ? focus : "General Fitness";

                        // Build query text for workout similarity search
                        string workoutQuery = $"Type: Workout | Target: {todayScheduledFocus} | Recovery: {userContext.RecoveryScore}/100 | Sleep: {userContext.VitalsSleepTotal} (Score: {userContext.SleepScore}) | HRV: {userContext.VitalsHrv}ms | RHR: {userContext.VitalsRhr}bpm";
                        var workoutQueryEmbedding = await _embeddingService.GenerateEmbeddingAsync(workoutQuery);

                        var workoutResults = await _vectorStore.SearchSimilarAsync(userId, "workout", workoutQueryEmbedding);
                        workoutSimilarContext = BuildSimilarPlansSection(workoutResults);

                        // Build query for diet similarity
                        string dietQuery = $"Type: Diet | Target: {todayScheduledFocus} | BMR: {userContext.InBodyBmr} | Active Burn: {userContext.VitalsCalories} | Weight: {userContext.InBodyWeight}kg";
                        var dietQueryEmbedding = await _embeddingService.GenerateEmbeddingAsync(dietQuery);

                        var dietResults = await _vectorStore.SearchSimilarAsync(userId, "diet", dietQueryEmbedding);
                        dietSimilarContext = BuildSimilarPlansSection(dietResults);

                        // Query weekly digest store for long-term behavioral patterns
                        string digestQuery = $"Weekly behavior | Mood: {userContext.DiaryBrief} | Target: {todayScheduledFocus} | Recovery: {userContext.RecoveryScore}";
                        var digestEmbedding = await _embeddingService.GenerateEmbeddingAsync(digestQuery);
                        var digestResults = await _vectorStore.SearchSimilarAsync(userId, "diary_digest", digestEmbedding, topK: 3, minScore: 0.55f);
                        digestContext = BuildDigestSection(digestResults);

                        // Get recent feedback for context
                        var recentFeedback = await storageRepo.GetRecentFeedbackAsync(userId, 5);
                        if (recentFeedback.Count > 0)
                        {
                            var workoutFb = recentFeedback.Where(f => f.PlanType == "workout").Take(3).ToList();
                            var dietFb = recentFeedback.Where(f => f.PlanType == "diet").Take(3).ToList();
                            workoutFeedbackContext = BuildFeedbackSection(workoutFb);
                            dietFeedbackContext = BuildFeedbackSection(dietFb);
                        }

                        _logger.LogInformation("[Orchestrator] Vector context loaded: {WorkoutHits} workout, {DietHits} diet, {DigestHits} digest hits, {FeedbackCount} feedback",
                            workoutResults.Count, dietResults.Count, digestResults.Count, recentFeedback.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[Orchestrator] Vector store query failed — proceeding without historical context");
                    }
                }

                // Run the AI Agent (Workout)
                progress?.Report((PlanGenerationStatus.GeneratingWorkout, "Generating workout plan..."));
                string workoutJsonString = await aiAgent.GenerateWorkoutAsync(userContext, workoutSimilarContext, workoutFeedbackContext, digestContext);

                // Run the AI Dietician (Nutrition - JSON)
                progress?.Report((PlanGenerationStatus.GeneratingDiet, "Generating nutrition plan..."));
                string dietJsonString = await aiAgent.GenerateRecoveryDietJsonAsync(workoutJsonString, userContext, dietSimilarContext, dietFeedbackContext, digestContext);

                // Post-generation validation: check for excluded foods
                progress?.Report((PlanGenerationStatus.ValidatingDiet, "Validating nutrition plan..."));
                if (userContext.ExcludedFoods.Count > 0 && !string.IsNullOrEmpty(dietJsonString) && dietJsonString != "{}")
                {
                    var violations = userContext.ExcludedFoods
                        .Where(excluded => dietJsonString.Contains(excluded, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (violations.Count > 0)
                    {
                        _logger.LogWarning("[Orchestrator] Diet validation FAILED — found excluded foods: {Foods}. Re-generating.", string.Join(", ", violations));
                        string correctionNote = $"\n\nCRITICAL CORRECTION: Your previous plan contained BANNED foods: {string.Join(", ", violations)}. These are STRICTLY EXCLUDED. Regenerate the plan without any of these items. This is non-negotiable.\n";
                        dietJsonString = await aiAgent.GenerateRecoveryDietJsonAsync(workoutJsonString + correctionNote, userContext, dietSimilarContext, dietFeedbackContext, digestContext);
                    }
                }

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
                WorkoutPlan? workoutPlan = null;
                if (!string.IsNullOrEmpty(workoutJsonString) && workoutJsonString != "{}")
                {
                    try
                    {
                        var workoutData = JsonDocument.Parse(workoutJsonString).RootElement;
                        workoutMarkdown = FormatWorkoutJsonToMarkdown(workoutData);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[AiOrchestrator] Workout JSON Formatting Failed");
                        workoutMarkdown = "# Workout Plan\n\n*Your plan was generated but could not be formatted. Please try generating again.*";
                    }

                    // Deserialize to typed model separately — failure here must not break markdown
                    try
                    {
                        workoutPlan = JsonSerializer.Deserialize<WorkoutPlan>(workoutJsonString);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[AiOrchestrator] WorkoutPlan deserialization failed — diary will use manual entry fallback");
                    }
                }

                string finalMarkdown = workoutMarkdown + "\n\n---\n\n" + dietMarkdown;

                progress?.Report((PlanGenerationStatus.SavingPlans, "Saving plans..."));

                // Save to History (Workout & Diet)
                var weeklyHistory = await storageRepo.GetWeeklyHistoryAsync(userId) ?? new WeeklyWorkoutHistory();
                var weeklyDietHistory = await storageRepo.GetWeeklyDietHistoryAsync(userId) ?? new WeeklyDietHistory();

                weeklyHistory.PastWorkouts[todayString] = finalMarkdown;
                if (workoutPlan != null)
                    weeklyHistory.PastWorkoutPlans[todayString] = workoutPlan;
                await storageRepo.SaveWeeklyHistoryAsync(userId, weeklyHistory);

                if (dietPlan != null)
                {
                    weeklyDietHistory.PastDiets[todayString] = dietPlan;
                    await storageRepo.SaveWeeklyDietHistoryAsync(userId, weeklyDietHistory);
                }

                // Embed and store plans in vector store for future semantic retrieval
                if (_vectorStore is not null && _embeddingService is not null)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            string todayDay2 = appNow.ToString("dddd");
                            string muscleGroup = userContext.WorkoutSchedule.TryGetValue(todayDay2, out var mg) ? mg : "General";

                            if (!string.IsNullOrEmpty(workoutJsonString) && workoutJsonString != "{}")
                            {
                                string workoutTitle = ExtractWorkoutTitle(workoutJsonString);
                                string embeddingText = $"Type: Workout | Target: {muscleGroup} | Recovery: {userContext.RecoveryScore}/100 | Sleep: {userContext.VitalsSleepTotal} (Score: {userContext.SleepScore}) | HRV: {userContext.VitalsHrv}ms | RHR: {userContext.VitalsRhr}bpm | Exercises: {workoutTitle}";
                                var embedding = await _embeddingService.GenerateEmbeddingAsync(embeddingText);

                                var workoutRecord = new PlanRecord
                                {
                                    Id = $"{userId}_{todayString}_workout",
                                    UserId = userId,
                                    PlanDate = appNow,
                                    PlanType = "workout",
                                    MuscleGroup = muscleGroup,
                                    PlanJson = workoutJsonString,
                                    PlanSummary = workoutTitle,
                                    RecoveryScore = userContext.RecoveryScore,
                                    SleepScore = userContext.SleepScore,
                                    ActiveScore = userContext.ActiveScore,
                                    SleepTotal = userContext.VitalsSleepTotal,
                                    Rhr = userContext.VitalsRhr,
                                    Hrv = userContext.VitalsHrv
                                };

                                await _vectorStore.UpsertAsync(userId, workoutRecord, embedding);
                            }

                            if (dietPlan is not null)
                            {
                                string dietSummary = $"{dietPlan.TotalCaloriesTarget} kcal, {dietPlan.Meals.Count} meals";
                                string dietEmbeddingText = $"Type: Diet | Target: {muscleGroup} | BMR: {userContext.InBodyBmr} | Active Burn: {userContext.VitalsCalories} | Weight: {userContext.InBodyWeight}kg | Plan: {dietSummary}";
                                var dietEmbedding = await _embeddingService.GenerateEmbeddingAsync(dietEmbeddingText);

                                var dietRecord = new PlanRecord
                                {
                                    Id = $"{userId}_{todayString}_diet",
                                    UserId = userId,
                                    PlanDate = appNow,
                                    PlanType = "diet",
                                    MuscleGroup = muscleGroup,
                                    PlanJson = dietJsonString,
                                    PlanSummary = dietSummary,
                                    RecoveryScore = userContext.RecoveryScore,
                                    SleepScore = userContext.SleepScore,
                                    ActiveScore = userContext.ActiveScore,
                                    SleepTotal = userContext.VitalsSleepTotal,
                                    Rhr = userContext.VitalsRhr,
                                    Hrv = userContext.VitalsHrv
                                };

                                await _vectorStore.UpsertAsync(userId, dietRecord, dietEmbedding);
                            }

                            _logger.LogInformation("[Orchestrator] Plans embedded and stored in vector store for {UserId}", userId);

                            // Generate weekly digest if a full week has passed
                            await TryGenerateWeeklyDigestAsync(userId, appNow, storageRepo);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "[Orchestrator] Failed to embed plans in vector store — plans are still saved normally");
                        }
                    });
                }

                // Send the Email (Combined)
                if (sendEmail)
                {
                    progress?.Report((PlanGenerationStatus.SendingNotification, "Sending email notification..."));
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

        private static string GetStringOrDefault(JsonElement element, string propertyName, string fallback = "")
        {
            if (element.TryGetProperty(propertyName, out var prop))
            {
                return prop.ValueKind == JsonValueKind.String ? prop.GetString() ?? fallback : prop.ToString();
            }
            return fallback;
        }

        private static string FormatWorkoutJsonToMarkdown(JsonElement workoutData)
        {
            var sb = new System.Text.StringBuilder();

            string title = GetStringOrDefault(workoutData, "session_title", "Workout Plan");
            string date = GetStringOrDefault(workoutData, "plan_date");

            sb.AppendLine($"# {title}");
            if (!string.IsNullOrEmpty(date))
                sb.AppendLine($"<p style='font-size: 0.85rem; color: #6b7280; margin-top: -10px;'>Date: {date}</p>\n");

            if (workoutData.TryGetProperty("personalized_introduction", out var intro) && intro.ValueKind == JsonValueKind.String)
            {
                sb.AppendLine($"{intro.GetString()}\n");
            }

            if (workoutData.TryGetProperty("warmup", out var warmup) && warmup.ValueKind == JsonValueKind.Array)
            {
                sb.AppendLine("## Warmup");
                foreach (var item in warmup.EnumerateArray())
                {
                    string exercise = GetStringOrDefault(item, "exercise", "Exercise");
                    string instruction = GetStringOrDefault(item, "instruction");
                    sb.AppendLine($"- **{exercise}**{(string.IsNullOrEmpty(instruction) ? "" : $": {instruction}")}");
                }
            }

            if (workoutData.TryGetProperty("main_workout", out var mainWorkout) && mainWorkout.ValueKind == JsonValueKind.Array)
            {
                sb.AppendLine("\n## Main Workout");
                sb.AppendLine("| Exercise | Sets | Reps | Rest | Notes |");
                sb.AppendLine("| :--- | :--- | :--- | :--- | :--- |");
                foreach (var item in mainWorkout.EnumerateArray())
                {
                    string exercise = GetStringOrDefault(item, "exercise", "Exercise");
                    string sets = GetStringOrDefault(item, "sets", "-");
                    string reps = GetStringOrDefault(item, "reps", "-");
                    string rest = GetStringOrDefault(item, "rest", "-");
                    string notes = GetStringOrDefault(item, "notes");
                    sb.AppendLine($"| {exercise} | {sets} | {reps} | {rest} | {notes} |");
                }
            }

            if (workoutData.TryGetProperty("cooldown", out var cooldown) && cooldown.ValueKind == JsonValueKind.Array)
            {
                sb.AppendLine("\n## Cooldown");
                foreach (var item in cooldown.EnumerateArray())
                {
                    string exercise = GetStringOrDefault(item, "exercise", "Exercise");
                    string duration = GetStringOrDefault(item, "duration");
                    sb.AppendLine($"- **{exercise}**{(string.IsNullOrEmpty(duration) ? "" : $" ({duration})")}");
                }
            }

            string coachNotes = GetStringOrDefault(workoutData, "coach_notes");
            if (!string.IsNullOrEmpty(coachNotes))
            {
                sb.AppendLine($"\n---");
                sb.AppendLine($"**Coach Notes**: {coachNotes}");
            }

            sb.AppendLine("\nStay strong, <br><br>**Apex** <br>*Your AI Biomechanics Specialist*");

            return sb.ToString();
        }

        /// <summary>
        /// Builds a concise text section from similar historical plans for the AI prompt.
        /// </summary>
        private static string BuildSimilarPlansSection(List<PlanSearchResult> results)
        {
            if (results.Count == 0) return "";

            var sb = new StringBuilder();
            foreach (var r in results)
            {
                sb.Append($"- [{r.Record.PlanDate:MMM dd}] {r.Record.MuscleGroup} ");
                sb.Append($"(Recovery: {r.Record.RecoveryScore}, Sleep: {r.Record.SleepScore}) ");
                sb.Append($"Rating: {r.Record.Feedback?.Rating ?? 0}/5 ");
                sb.AppendLine($"Similarity: {r.Score:P0}");
                sb.AppendLine($"  Summary: {r.Record.PlanSummary}");
                if (r.Record.Feedback is not null)
                {
                    if (r.Record.Feedback.SkippedItems.Count > 0)
                        sb.AppendLine($"  Skipped: {string.Join(", ", r.Record.Feedback.SkippedItems)}");
                    if (!string.IsNullOrEmpty(r.Record.Feedback.Note))
                        sb.AppendLine($"  User note: {r.Record.Feedback.Note}");
                    if (r.Record.Feedback.Difficulty is not "just-right")
                        sb.AppendLine($"  Difficulty feedback: {r.Record.Feedback.Difficulty}");
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Builds a concise recent feedback summary for the AI prompt.
        /// </summary>
        private static string BuildFeedbackSection(List<PlanFeedback> feedbacks)
        {
            if (feedbacks.Count == 0) return "";

            var sb = new StringBuilder();
            foreach (var f in feedbacks)
            {
                sb.Append($"- {f.FeedbackDate:MMM dd} ({f.PlanType}): Rated {f.Rating}/5, difficulty: {f.Difficulty}");
                if (f.SkippedItems.Count > 0)
                    sb.Append($", skipped: {string.Join(", ", f.SkippedItems)}");
                if (!string.IsNullOrEmpty(f.Note))
                    sb.Append($", note: \"{f.Note}\"");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        /// <summary>
        /// Extracts the session title and exercise names from workout JSON for embedding text.
        /// </summary>
        private static string ExtractWorkoutTitle(string workoutJson)
        {
            try
            {
                var doc = JsonDocument.Parse(workoutJson).RootElement;
                string title = GetStringOrDefault(doc, "session_title", "Workout");

                if (doc.TryGetProperty("main_workout", out var main) && main.ValueKind == JsonValueKind.Array)
                {
                    var exercises = main.EnumerateArray()
                        .Select(e => GetStringOrDefault(e, "exercise"))
                        .Where(e => !string.IsNullOrEmpty(e))
                        .Take(5);
                    return $"{title} — {string.Join(", ", exercises)}";
                }

                return title;
            }
            catch
            {
                return "Workout";
            }
        }

        /// <summary>
        /// Builds a concise text section from weekly digest search results for the AI prompt.
        /// </summary>
        private static string BuildDigestSection(List<PlanSearchResult> results)
        {
            if (results.Count == 0) return "";

            var sb = new StringBuilder();
            foreach (var r in results)
            {
                sb.AppendLine($"- [{r.Record.PlanDate:MMM dd} week] (Similarity: {r.Score:P0})");
                sb.AppendLine($"  {r.Record.PlanSummary}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Checks if a weekly digest should be generated for the previous week.
        /// Generates, embeds, and stores the digest if it doesn't exist yet.
        /// </summary>
        private async Task TryGenerateWeeklyDigestAsync(string userId, DateTime appNow, IStorageRepository storageRepo)
        {
            try
            {
                // Calculate last week's Monday
                var today = appNow.Date;
                int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7; // Mon=0, Tue=1, ..., Sun=6
                var thisMonday = today.AddDays(-daysSinceMonday);
                var lastMonday = thisMonday.AddDays(-7);
                var lastSunday = thisMonday.AddDays(-1);

                string weekStart = lastMonday.ToString("yyyy-MM-dd");

                // Check if digest already exists for last week
                var existingDigest = await storageRepo.GetWeeklyDigestAsync(userId, weekStart);
                if (existingDigest != null)
                    return; // Already generated

                // Load diary entries for last week (need 14 days and filter)
                var allEntries = await storageRepo.GetRecentDiaryEntriesAsync(userId, 14);
                var weekEntries = allEntries
                    .Where(e => DateTime.TryParse(e.Date, out var d) && d >= lastMonday && d <= lastSunday)
                    .OrderBy(e => e.Date)
                    .ToList();

                if (weekEntries.Count < 2)
                {
                    _logger.LogInformation("[Orchestrator] Skipping weekly digest for {UserId} — only {Count} diary entries in week of {Week}",
                        userId, weekEntries.Count, weekStart);
                    return;
                }

                var digest = BuildWeeklyDigest(userId, weekStart, lastSunday.ToString("yyyy-MM-dd"), weekEntries);

                // Save to Firebase
                await storageRepo.SaveWeeklyDigestAsync(userId, digest);

                // Embed and upsert to Qdrant
                if (_embeddingService is not null && _vectorStore is not null)
                {
                    var embedding = await _embeddingService.GenerateEmbeddingAsync(digest.DigestText);

                    var record = new PlanRecord
                    {
                        Id = $"{userId}_{weekStart}_digest",
                        UserId = userId,
                        PlanDate = lastMonday,
                        PlanType = "diary_digest",
                        MuscleGroup = "",
                        PlanJson = JsonSerializer.Serialize(digest),
                        PlanSummary = digest.DigestText,
                        RecoveryScore = 0,
                        SleepScore = 0,
                        ActiveScore = 0,
                        SleepTotal = "",
                        Rhr = "",
                        Hrv = ""
                    };

                    await _vectorStore.UpsertAsync(userId, record, embedding);
                }

                _logger.LogInformation("[Orchestrator] Weekly digest generated for {UserId} week of {Week} ({Days} diary days)",
                    userId, weekStart, digest.DiaryDays);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Orchestrator] Failed to generate weekly digest for {UserId}", userId);
            }
        }

        /// <summary>
        /// Aggregates a week of diary entries into a single WeeklyDigest with behavioral patterns.
        /// </summary>
        private static WeeklyDigest BuildWeeklyDigest(string userId, string weekStart, string weekEnd, List<DailyDiary> entries)
        {
            var allMeals = entries.SelectMany(e => e.ActualMeals).ToList();
            var allWorkouts = entries.SelectMany(e => e.WorkoutLog).ToList();
            var allPains = entries.SelectMany(e => e.PainLog).ToList();

            int totalDone = allWorkouts.Count(w => w.Completed);
            int totalSkipped = allWorkouts.Count(w => w.Feeling == "skipped");
            int totalWorkoutEntries = allWorkouts.Count;

            double avgMood = entries.Where(e => e.MoodEnergy > 0).Select(e => (double)e.MoodEnergy).DefaultIfEmpty(0).Average();
            double avgWater = entries.Where(e => e.WaterIntakeLitres > 0).Select(e => e.WaterIntakeLitres).DefaultIfEmpty(0).Average();

            // Find foods that appear 3+ times across the week (consistent meals)
            var mealFrequency = allMeals
                .Where(m => !string.IsNullOrWhiteSpace(m.FoodName))
                .GroupBy(m => m.FoodName.ToLowerInvariant().Trim())
                .Where(g => g.Count() >= 3)
                .Select(g => g.Key)
                .ToList();

            // Find body areas with pain on 2+ days (recurring pains)
            var painAreas = allPains
                .Where(p => !string.IsNullOrWhiteSpace(p.BodyArea))
                .GroupBy(p => p.BodyArea.ToLowerInvariant().Trim())
                .Where(g => g.Count() >= 2)
                .Select(g => $"{g.Key} (avg {g.Average(p => p.Severity):0.0}/5)")
                .ToList();

            // Find exercises frequently skipped
            var skippedExercises = allWorkouts
                .Where(w => w.Feeling == "skipped" && !string.IsNullOrWhiteSpace(w.Exercise))
                .GroupBy(w => w.Exercise.ToLowerInvariant().Trim())
                .Where(g => g.Count() >= 2)
                .Select(g => g.Key)
                .ToList();

            double workoutPct = totalWorkoutEntries > 0 ? (double)totalDone / totalWorkoutEntries * 100 : 0;

            // Build the digest narrative for embedding
            var sb = new StringBuilder();
            sb.Append($"Week of {weekStart} to {weekEnd}: ");
            sb.Append($"{entries.Count}/7 days logged. ");
            sb.Append($"Mood avg {avgMood:0.0}/5, Water avg {avgWater:0.0}L/day. ");
            sb.Append($"Workout adherence: {totalDone}/{totalWorkoutEntries} exercises done ({workoutPct:0}%). ");

            if (mealFrequency.Count > 0)
                sb.Append($"Consistent meals: {string.Join(", ", mealFrequency)}. ");

            if (painAreas.Count > 0)
                sb.Append($"Recurring pain: {string.Join(", ", painAreas)}. ");

            if (skippedExercises.Count > 0)
                sb.Append($"Frequently skipped: {string.Join(", ", skippedExercises)}. ");

            // Add notable general notes
            var notes = entries
                .Where(e => !string.IsNullOrWhiteSpace(e.GeneralNotes))
                .Select(e => e.GeneralNotes.Trim())
                .Take(3);
            if (notes.Any())
                sb.Append($"User notes: {string.Join("; ", notes)}. ");

            return new WeeklyDigest
            {
                Id = $"{userId}_{weekStart}_digest",
                UserId = userId,
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                DiaryDays = entries.Count,
                DigestText = sb.ToString().Trim(),
                AvgMood = Math.Round(avgMood, 1),
                AvgWater = Math.Round(avgWater, 1),
                WorkoutCompletionPct = Math.Round(workoutPct, 0),
                TotalExercisesDone = totalDone,
                TotalExercisesSkipped = totalSkipped,
                RecurringPains = painAreas,
                ConsistentMeals = mealFrequency,
                FrequentlySkipped = skippedExercises
            };
        }
    }
}
