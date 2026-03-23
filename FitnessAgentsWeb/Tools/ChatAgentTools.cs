using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace FitnessAgentsWeb.Tools
{
    /// <summary>
    /// Tool functions exposed to the chat agent for reading and modifying user data.
    /// All write tools perform read-then-merge to avoid overwriting existing data.
    /// </summary>
    public class ChatAgentTools
    {
        private readonly IStorageRepository _storage;
        private readonly IHealthDataProcessor _healthProcessor;
        private readonly string _userId;
        private readonly ILogger _logger;

        public ChatAgentTools(
            IStorageRepository storage,
            IHealthDataProcessor healthProcessor,
            string userId,
            ILogger logger)
        {
            _storage = storage;
            _healthProcessor = healthProcessor;
            _userId = userId;
            _logger = logger;
        }

        // ═══════════════════════════════════════════════════════════════
        // READ TOOLS — Agent must call these to understand current state
        // ═══════════════════════════════════════════════════════════════

        [Description("Gets the user's current profile including food preferences, excluded foods, cuisine style, cooking oils, staple grains, workout schedule, and personal details. ALWAYS call this before making any profile updates.")]
        public async Task<string> GetUserProfile()
        {
            var profile = await _storage.GetUserProfileAsync(_userId);
            if (profile is null) return "No profile found for this user.";

            var sb = new StringBuilder();
            sb.AppendLine($"Name: {profile.FirstName} {profile.LastName}");
            sb.AppendLine($"Age: {profile.Age}");
            sb.AppendLine($"Food Preferences: {profile.FoodPreferences}");
            sb.AppendLine($"Excluded Foods: {(profile.ExcludedFoods.Count > 0 ? string.Join(", ", profile.ExcludedFoods) : "None")}");
            sb.AppendLine($"Cuisine Style: {(string.IsNullOrEmpty(profile.CuisineStyle) ? "Not set" : profile.CuisineStyle)}");
            sb.AppendLine($"Cooking Oils: {(profile.CookingOils.Count > 0 ? string.Join(", ", profile.CookingOils) : "Not specified")}");
            sb.AppendLine($"Staple Grains: {(profile.StapleGrains.Count > 0 ? string.Join(", ", profile.StapleGrains) : "Not specified")}");
            sb.AppendLine($"Conditions/Injuries: {profile.Preferences}");
            sb.AppendLine("Workout Schedule:");
            foreach (var day in profile.WorkoutSchedule)
                sb.AppendLine($"  {day.Key}: {day.Value}");
            return sb.ToString();
        }

        [Description("Gets today's diary entry including meals eaten, workout log, pain log, mood, and water intake. ALWAYS call this before updating diary entries to merge with existing data.")]
        public async Task<string> GetTodayDiary()
        {
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var diary = await _storage.GetDiaryEntryAsync(_userId, today);
            if (diary is null) return "No diary entry for today yet.";

            var sb = new StringBuilder();
            sb.AppendLine($"Date: {diary.Date}");
            sb.AppendLine($"Mood/Energy: {diary.MoodEnergy}/5");
            sb.AppendLine($"Water Intake: {diary.WaterIntakeLitres}L");

            if (diary.ActualMeals.Count > 0)
            {
                sb.AppendLine("Meals:");
                foreach (var meal in diary.ActualMeals)
                    sb.AppendLine($"  [{meal.MealTime}] {meal.FoodName} ({meal.Quantity}){(meal.WasFromPlan ? " ✓plan" : "")}");
            }

            if (diary.WorkoutLog.Count > 0)
            {
                sb.AppendLine("Workout Log:");
                foreach (var log in diary.WorkoutLog)
                    sb.AppendLine($"  {log.Exercise}: {(log.Completed ? "Done" : "Skipped")} ({log.Feeling}){(string.IsNullOrEmpty(log.Notes) ? "" : $" - {log.Notes}")}");
            }

            if (diary.PainLog.Count > 0)
            {
                sb.AppendLine("Pain Log:");
                foreach (var pain in diary.PainLog)
                    sb.AppendLine($"  {pain.BodyArea}: {pain.Severity}/5 - {pain.Description}");
            }

            if (!string.IsNullOrEmpty(diary.SleepNotes)) sb.AppendLine($"Sleep Notes: {diary.SleepNotes}");
            if (!string.IsNullOrEmpty(diary.GeneralNotes)) sb.AppendLine($"General Notes: {diary.GeneralNotes}");
            return sb.ToString();
        }

        [Description("Gets the user's current health metrics including sleep, heart rate, HRV, steps, calories, recovery score, body composition, and trend data.")]
        public async Task<string> GetHealthInsights()
        {
            var healthData = await _storage.GetTodayHealthDataAsync(_userId);
            if (healthData is null) return "No health data available for today.";

            var context = await _healthProcessor.LoadHealthStateToRAMAsync(_userId, healthData);

            var sb = new StringBuilder();
            sb.AppendLine("=== Today's Health Snapshot ===");
            sb.AppendLine($"Sleep: {context.VitalsSleepTotal} (Deep: {context.VitalsSleepDeep}) | Score: {context.SleepScore}/100");
            sb.AppendLine($"Resting HR: {context.VitalsRhr} | HRV: {context.VitalsHrv}");
            sb.AppendLine($"Steps: {context.VitalsSteps} | Distance: {context.VitalsDistance}");
            sb.AppendLine($"Calories: Active {context.VitalsCalories} | Total {context.VitalsTotalCalories}");
            sb.AppendLine($"Recovery Score: {context.RecoveryScore}/100 | Active Score: {context.ActiveScore}/100");
            sb.AppendLine($"SpO2: {context.VitalsSpO2} | VO2Max: {context.VitalsVo2Max}");
            sb.AppendLine($"Hydration: {context.VitalsHydration}L");
            sb.AppendLine();
            sb.AppendLine("=== Body Composition ===");
            sb.AppendLine($"Weight: {context.InBodyWeight}kg | Body Fat: {context.InBodyBf}%");
            sb.AppendLine($"SMM: {context.InBodySmm}kg | BMR: {context.InBodyBmr}kcal | BMI: {context.InBodyBmi}");
            sb.AppendLine($"Fat Control Target: {context.InBodyFatControl}kg | Muscle Control: {context.InBodyMuscleControl}kg");
            sb.AppendLine();
            sb.AppendLine("=== 15-Day Averages ===");
            sb.AppendLine($"Avg RHR: {context.AvgRhr15Day} | Avg HRV: {context.AvgHrv15Day} | Avg Steps: {context.AvgSteps15Day} | Avg Sleep: {context.AvgSleep15Day}");
            return sb.ToString();
        }

        [Description("Gets today's workout plan and diet plan.")]
        public async Task<string> GetTodayPlans()
        {
            var sb = new StringBuilder();

            var workoutHistory = await _storage.GetWeeklyHistoryAsync(_userId);
            string todayDay = DateTime.UtcNow.DayOfWeek.ToString();
            if (workoutHistory?.PastWorkoutPlans is not null && workoutHistory.PastWorkoutPlans.TryGetValue(todayDay, out var workout))
            {
                sb.AppendLine("=== Today's Workout ===");
                sb.AppendLine($"Session: {workout.SessionTitle}");
                if (workout.Warmup?.Count > 0)
                {
                    sb.AppendLine("Warmup:");
                    foreach (var w in workout.Warmup) sb.AppendLine($"  - {w.Exercise}: {w.Instruction}");
                }
                if (workout.MainWorkout?.Count > 0)
                {
                    sb.AppendLine("Main Workout:");
                    foreach (var m in workout.MainWorkout) sb.AppendLine($"  - {m.Exercise}: {m.Sets}x{m.Reps} (Rest: {m.Rest}) {m.Notes}");
                }
                if (workout.Cooldown?.Count > 0)
                {
                    sb.AppendLine("Cooldown:");
                    foreach (var c in workout.Cooldown) sb.AppendLine($"  - {c.Exercise}: {c.Duration}");
                }
                if (!string.IsNullOrEmpty(workout.CoachNotes)) sb.AppendLine($"Coach Notes: {workout.CoachNotes}");
            }
            else
            {
                sb.AppendLine("No workout plan generated for today.");
            }

            var diet = await _storage.GetLatestDietAsync(_userId);
            if (diet is not null)
            {
                sb.AppendLine();
                sb.AppendLine("=== Today's Diet Plan ===");
                sb.AppendLine($"Target Calories: {diet.TotalCaloriesTarget}");
                foreach (var meal in diet.Meals)
                    sb.AppendLine($"  [{meal.MealType}] {meal.FoodName} - {meal.QuantityDescription} ({meal.Calories} cal)");
                if (!string.IsNullOrEmpty(diet.AiSummary)) sb.AppendLine($"Summary: {diet.AiSummary}");
            }
            else
            {
                sb.AppendLine("No diet plan generated for today.");
            }

            return sb.ToString();
        }

        [Description("Gets the recent 7-day diary history showing meals, workouts, pain, mood, and water intake patterns.")]
        public async Task<string> GetRecentDiaryHistory()
        {
            var entries = await _storage.GetRecentDiaryEntriesAsync(_userId, 7);
            if (entries.Count == 0) return "No diary entries in the last 7 days.";

            var sb = new StringBuilder();
            foreach (var entry in entries)
            {
                sb.AppendLine($"--- {entry.Date} (Mood: {entry.MoodEnergy}/5, Water: {entry.WaterIntakeLitres}L) ---");
                if (entry.ActualMeals.Count > 0)
                    sb.AppendLine($"  Meals: {string.Join(", ", entry.ActualMeals.Select(m => m.FoodName))}");
                if (entry.WorkoutLog.Count > 0)
                    sb.AppendLine($"  Exercises: {string.Join(", ", entry.WorkoutLog.Select(w => $"{w.Exercise}({w.Feeling})"))}");
                if (entry.PainLog.Count > 0)
                    sb.AppendLine($"  Pain: {string.Join(", ", entry.PainLog.Select(p => $"{p.BodyArea}:{p.Severity}/5"))}");
            }
            return sb.ToString();
        }

        // ═══════════════════════════════════════════════════════════════
        // WRITE TOOLS — Always read current state first, then merge
        // ═══════════════════════════════════════════════════════════════

        [Description("Updates the user's food preferences. Merges with existing data — does not replace. Provide only the fields that need changing. Fields: excludedFoodsToAdd (comma-separated foods to add to exclusion list), excludedFoodsToRemove (comma-separated foods to remove from exclusion list), cuisineStyle (string), cookingOilsToSet (comma-separated, replaces current), stapleGrainsToSet (comma-separated, replaces current), foodPreferences (free-text general preferences). MUST call GetUserProfile first to see current state.")]
        public async Task<string> UpdateFoodPreferences(
            string? excludedFoodsToAdd = null,
            string? excludedFoodsToRemove = null,
            string? cuisineStyle = null,
            string? cookingOilsToSet = null,
            string? stapleGrainsToSet = null,
            string? foodPreferences = null)
        {
            var profile = await _storage.GetUserProfileAsync(_userId);
            if (profile is null) return "Error: User profile not found.";

            var changes = new List<string>();

            if (!string.IsNullOrEmpty(excludedFoodsToAdd))
            {
                var toAdd = excludedFoodsToAdd.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var food in toAdd)
                {
                    if (!profile.ExcludedFoods.Contains(food, StringComparer.OrdinalIgnoreCase))
                        profile.ExcludedFoods.Add(food);
                }
                changes.Add($"Added to excluded foods: {string.Join(", ", toAdd)}");
            }

            if (!string.IsNullOrEmpty(excludedFoodsToRemove))
            {
                var toRemove = excludedFoodsToRemove.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                profile.ExcludedFoods.RemoveAll(f => toRemove.Contains(f, StringComparer.OrdinalIgnoreCase));
                changes.Add($"Removed from excluded foods: {string.Join(", ", toRemove)}");
            }

            if (!string.IsNullOrEmpty(cuisineStyle))
            {
                profile.CuisineStyle = cuisineStyle;
                changes.Add($"Cuisine style set to: {cuisineStyle}");
            }

            if (!string.IsNullOrEmpty(cookingOilsToSet))
            {
                profile.CookingOils = cookingOilsToSet.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                changes.Add($"Cooking oils updated to: {string.Join(", ", profile.CookingOils)}");
            }

            if (!string.IsNullOrEmpty(stapleGrainsToSet))
            {
                profile.StapleGrains = stapleGrainsToSet.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                changes.Add($"Staple grains updated to: {string.Join(", ", profile.StapleGrains)}");
            }

            if (!string.IsNullOrEmpty(foodPreferences))
            {
                profile.FoodPreferences = foodPreferences;
                changes.Add($"Food preferences updated to: {foodPreferences}");
            }

            if (changes.Count == 0) return "No changes specified.";

            await _storage.SaveUserProfileAsync(_userId, profile);
            _logger.LogInformation("[ChatAgent] Updated food preferences for {UserId}: {Changes}", _userId, string.Join("; ", changes));
            return $"Profile updated successfully:\n{string.Join("\n", changes)}";
        }

        [Description("Updates the user's conditions, pain points, or injury information in their profile. MUST call GetUserProfile first. Parameter: conditions (the updated conditions/injuries text).")]
        public async Task<string> UpdateConditions(string conditions)
        {
            var profile = await _storage.GetUserProfileAsync(_userId);
            if (profile is null) return "Error: User profile not found.";

            profile.Preferences = conditions;
            await _storage.SaveUserProfileAsync(_userId, profile);
            _logger.LogInformation("[ChatAgent] Updated conditions for {UserId}", _userId);
            return $"Conditions updated to: {conditions}";
        }

        [Description("Updates the user's weekly workout schedule. Provide day-focus pairs. MUST call GetUserProfile first to see current schedule. Parameters: monday, tuesday, wednesday, thursday, friday, saturday, sunday (each optional, e.g. 'Chest and Triceps', 'Rest Day', 'Fasting').")]
        public async Task<string> UpdateWorkoutSchedule(
            string? monday = null, string? tuesday = null, string? wednesday = null,
            string? thursday = null, string? friday = null, string? saturday = null,
            string? sunday = null)
        {
            var profile = await _storage.GetUserProfileAsync(_userId);
            if (profile is null) return "Error: User profile not found.";

            var changes = new List<string>();
            var updates = new Dictionary<string, string?>
            {
                ["Monday"] = monday, ["Tuesday"] = tuesday, ["Wednesday"] = wednesday,
                ["Thursday"] = thursday, ["Friday"] = friday, ["Saturday"] = saturday, ["Sunday"] = sunday
            };

            foreach (var (day, focus) in updates)
            {
                if (!string.IsNullOrEmpty(focus))
                {
                    profile.WorkoutSchedule[day] = focus;
                    changes.Add($"{day}: {focus}");
                }
            }

            if (changes.Count == 0) return "No schedule changes specified.";

            await _storage.SaveUserProfileAsync(_userId, profile);
            _logger.LogInformation("[ChatAgent] Updated workout schedule for {UserId}", _userId);
            return $"Workout schedule updated:\n{string.Join("\n", changes)}";
        }

        [Description("Adds or updates today's diary entry. Merges with existing data — does not replace. MUST call GetTodayDiary first to see what's already logged. Parameters: mealsJson (JSON array of {mealTime, foodName, quantity, wasFromPlan, substitution}), workoutLogJson (JSON array of {exercise, completed, feeling, notes}), painLogJson (JSON array of {bodyArea, severity, description}), moodEnergy (1-5), waterIntakeLitres (double), sleepNotes (string), generalNotes (string). All parameters optional — provide only what needs updating.")]
        public async Task<string> UpsertDiaryEntry(
            string? mealsJson = null,
            string? workoutLogJson = null,
            string? painLogJson = null,
            int? moodEnergy = null,
            double? waterIntakeLitres = null,
            string? sleepNotes = null,
            string? generalNotes = null)
        {
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var existing = await _storage.GetDiaryEntryAsync(_userId, today) ?? new DailyDiary { Date = today };
            var changes = new List<string>();

            if (!string.IsNullOrEmpty(mealsJson))
            {
                var newMeals = JsonSerializer.Deserialize<List<DiaryMeal>>(mealsJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (newMeals is not null)
                {
                    existing.ActualMeals.AddRange(newMeals);
                    changes.Add($"Added {newMeals.Count} meal(s)");
                }
            }

            if (!string.IsNullOrEmpty(workoutLogJson))
            {
                var newLogs = JsonSerializer.Deserialize<List<DiaryWorkoutLog>>(workoutLogJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (newLogs is not null)
                {
                    // Merge by exercise name — update existing entries, add new ones
                    foreach (var newLog in newLogs)
                    {
                        var existingLog = existing.WorkoutLog.FirstOrDefault(w =>
                            string.Equals(w.Exercise, newLog.Exercise, StringComparison.OrdinalIgnoreCase));
                        if (existingLog is not null)
                        {
                            existingLog.Completed = newLog.Completed;
                            existingLog.Feeling = newLog.Feeling;
                            if (!string.IsNullOrEmpty(newLog.Notes)) existingLog.Notes = newLog.Notes;
                        }
                        else
                        {
                            existing.WorkoutLog.Add(newLog);
                        }
                    }
                    changes.Add($"Updated {newLogs.Count} workout log(s)");
                }
            }

            if (!string.IsNullOrEmpty(painLogJson))
            {
                var newPains = JsonSerializer.Deserialize<List<DiaryPainLog>>(painLogJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (newPains is not null)
                {
                    existing.PainLog.AddRange(newPains);
                    changes.Add($"Added {newPains.Count} pain log(s)");
                }
            }

            if (moodEnergy.HasValue)
            {
                existing.MoodEnergy = Math.Clamp(moodEnergy.Value, 1, 5);
                changes.Add($"Mood/Energy set to {existing.MoodEnergy}/5");
            }

            if (waterIntakeLitres.HasValue)
            {
                existing.WaterIntakeLitres = waterIntakeLitres.Value;
                changes.Add($"Water intake set to {existing.WaterIntakeLitres}L");
            }

            if (!string.IsNullOrEmpty(sleepNotes))
            {
                existing.SleepNotes = sleepNotes;
                changes.Add("Sleep notes updated");
            }

            if (!string.IsNullOrEmpty(generalNotes))
            {
                existing.GeneralNotes = generalNotes;
                changes.Add("General notes updated");
            }

            if (changes.Count == 0) return "No diary changes specified.";

            existing.UpdatedAt = DateTime.UtcNow;
            await _storage.SaveDiaryEntryAsync(_userId, existing);
            _logger.LogInformation("[ChatAgent] Updated diary for {UserId}: {Changes}", _userId, string.Join("; ", changes));
            return $"Diary updated for {today}:\n{string.Join("\n", changes)}";
        }

        [Description("Submits feedback for today's workout or diet plan. Parameters: planType ('workout' or 'diet'), rating (1-5), difficulty ('too-easy', 'just-right', 'too-hard'), skippedItems (comma-separated exercise/meal names that were skipped), note (free-text feedback).")]
        public async Task<string> SubmitPlanFeedback(
            string planType,
            int rating,
            string difficulty = "just-right",
            string? skippedItems = null,
            string? note = null)
        {
            var feedback = new PlanFeedback
            {
                PlanId = $"{planType}_{DateTime.UtcNow:yyyy-MM-dd}",
                PlanType = planType,
                FeedbackDate = DateTime.UtcNow,
                Rating = Math.Clamp(rating, 1, 5),
                Difficulty = difficulty,
                SkippedItems = string.IsNullOrEmpty(skippedItems) ? new() : skippedItems.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                Note = note ?? string.Empty
            };

            await _storage.SavePlanFeedbackAsync(_userId, feedback);
            _logger.LogInformation("[ChatAgent] Plan feedback submitted for {UserId}: {PlanType} rated {Rating}/5", _userId, planType, rating);
            return $"Feedback saved for {planType} plan: {rating}/5 ({difficulty}){(string.IsNullOrEmpty(note) ? "" : $" — {note}")}";
        }
    }
}
