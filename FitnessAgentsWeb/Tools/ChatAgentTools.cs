using FitnessAgentsWeb.Core.Helpers;
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
        private readonly IAppNotificationStore _notifications;
        private readonly IPlanGenerationTracker _jobTracker;
        private readonly string _userId;
        private readonly ILogger _logger;

        public ChatAgentTools(
            IStorageRepository storage,
            IHealthDataProcessor healthProcessor,
            IAppNotificationStore notifications,
            IPlanGenerationTracker jobTracker,
            string userId,
            ILogger logger)
        {
            _storage = storage;
            _healthProcessor = healthProcessor;
            _notifications = notifications;
            _jobTracker = jobTracker;
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

        [Description("Gets detailed sleep breakdown including sleep stages (Deep, REM, Light, Awake), durations, sleep efficiency, WASO, bedtime/wake time, and vitals during sleep.")]
        public async Task<string> GetSleepDetails()
        {
            var healthData = await _storage.GetTodayHealthDataAsync(_userId);
            if (healthData?.Sleep is not { Count: > 0 })
                return "No sleep data available for today.";

            var tz = ResolveTimeZone();
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var targetDate = nowLocal.Date;
            var windowStart = targetDate.AddHours(-12);
            var windowEnd = targetDate.AddHours(12);

            var sessions = healthData.Sleep
                .Where(s => { var local = TimeZoneInfo.ConvertTimeFromUtc(s.SessionEndTime, tz); return local >= windowStart && local < windowEnd; })
                .ToList();
            if (sessions.Count == 0) return "No sleep sessions found for today's window.";

            var allStages = sessions.SelectMany(s => s.Stages).OrderBy(st => st.StartTime).ToList();
            if (allStages.Count == 0) return "Sleep data found but no stage information available.";

            // Stage codes: "1" = Awake, "6" = REM, "4" = Light, "5" = Deep
            int awakeSecs = allStages.Where(st => st.Stage == "1").Sum(st => st.DurationSeconds);
            int remSecs = allStages.Where(st => st.Stage == "6").Sum(st => st.DurationSeconds);
            int lightSecs = allStages.Where(st => st.Stage == "4").Sum(st => st.DurationSeconds);
            int deepSecs = allStages.Where(st => st.Stage == "5").Sum(st => st.DurationSeconds);
            int totalSleepSecs = remSecs + lightSecs + deepSecs;
            int totalTimeInBedSecs = sessions.Sum(s => s.DurationSeconds);
            int totalStageSecs = awakeSecs + totalSleepSecs;

            var sb = new StringBuilder();
            sb.AppendLine("=== Sleep Stage Breakdown ===");
            sb.AppendLine($"Deep Sleep: {FormatDuration(deepSecs)} ({(totalStageSecs > 0 ? Math.Round((double)deepSecs / totalStageSecs * 100) : 0)}%)");
            sb.AppendLine($"REM Sleep: {FormatDuration(remSecs)} ({(totalStageSecs > 0 ? Math.Round((double)remSecs / totalStageSecs * 100) : 0)}%)");
            sb.AppendLine($"Light Sleep: {FormatDuration(lightSecs)} ({(totalStageSecs > 0 ? Math.Round((double)lightSecs / totalStageSecs * 100) : 0)}%)");
            sb.AppendLine($"Awake: {FormatDuration(awakeSecs)} ({(totalStageSecs > 0 ? Math.Round((double)awakeSecs / totalStageSecs * 100) : 0)}%)");
            sb.AppendLine();
            sb.AppendLine("=== Key Metrics ===");
            sb.AppendLine($"Total Sleep: {FormatDuration(totalSleepSecs)}");
            sb.AppendLine($"Time in Bed: {FormatDuration(totalTimeInBedSecs)}");
            int targetSleepSecs = 8 * 3600;
            int debtSecs = Math.Max(0, targetSleepSecs - totalSleepSecs);
            sb.AppendLine($"Sleep Debt: {FormatDuration(debtSecs)} (vs 8h target)");

            double efficiency = totalTimeInBedSecs > 0 ? (double)totalSleepSecs / totalTimeInBedSecs * 100 : 0;
            sb.AppendLine($"Sleep Efficiency: {Math.Round(efficiency)}%");

            // WASO
            var sleepOnset = allStages.FirstOrDefault(st => st.Stage != "1" && st.Stage != "2");
            var lastSleep = allStages.LastOrDefault(st => st.Stage != "1" && st.Stage != "2");
            if (sleepOnset is not null && lastSleep is not null)
            {
                int wasoSecs = allStages
                    .Where(st => st.Stage == "1" && st.StartTime >= sleepOnset.StartTime && st.EndTime <= lastSleep.EndTime)
                    .Sum(st => st.DurationSeconds);
                sb.AppendLine($"WASO (Wake After Sleep Onset): {FormatDuration(wasoSecs)}");
            }

            // Bed/Wake times
            var earliest = allStages.OrderBy(st => st.StartTime).First();
            var latest = allStages.OrderByDescending(st => st.EndTime).First();
            sb.AppendLine($"Bedtime: {TimeZoneInfo.ConvertTimeFromUtc(earliest.StartTime, tz):hh:mm tt}");
            sb.AppendLine($"Wake Time: {TimeZoneInfo.ConvertTimeFromUtc(latest.EndTime, tz):hh:mm tt}");

            // HR during sleep
            if (healthData.HeartRate is { Count: > 0 })
            {
                var sleepHr = healthData.HeartRate
                    .Where(hr => hr.Time >= earliest.StartTime && hr.Time <= latest.EndTime)
                    .ToList();
                if (sleepHr.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("=== Vitals During Sleep ===");
                    sb.AppendLine($"Heart Rate: Avg {Math.Round(sleepHr.Average(h => h.Bpm))} | Min {sleepHr.Min(h => h.Bpm)} | Max {sleepHr.Max(h => h.Bpm)} bpm");
                }
            }
            if (healthData.HRV is { Count: > 0 })
            {
                var sleepHrv = healthData.HRV.Where(h => h.Time >= earliest.StartTime && h.Time <= latest.EndTime).ToList();
                if (sleepHrv.Count > 0)
                    sb.AppendLine($"HRV During Sleep: {Math.Round(sleepHrv.Average(h => h.Rmssd))} ms avg");
            }

            return sb.ToString();
        }

        [Description("Gets today's exercise sessions from Health Connect data — includes type, duration, start/end times. Shows what exercises the user actually performed (as tracked by their watch/phone).")]
        public async Task<string> GetExerciseHistory()
        {
            var healthData = await _storage.GetTodayHealthDataAsync(_userId);
            if (healthData?.Exercise is not { Count: > 0 })
                return "No exercise sessions recorded in Health Connect data.";

            var tz = ResolveTimeZone();
            var sb = new StringBuilder();
            sb.AppendLine($"=== Exercise Sessions ({healthData.Exercise.Count} total) ===");

            int totalMinutes = 0;
            foreach (var group in healthData.Exercise.OrderByDescending(e => e.StartTime).GroupBy(e => TimeZoneInfo.ConvertTimeFromUtc(e.StartTime, tz).Date))
            {
                sb.AppendLine($"\n--- {group.Key:ddd, MMM dd} ---");
                foreach (var e in group)
                {
                    string name = ExerciseTypeHelper.GetExerciseName(e.Type);
                    int durationMin = e.DurationSeconds / 60;
                    totalMinutes += durationMin;
                    string start = TimeZoneInfo.ConvertTimeFromUtc(e.StartTime, tz).ToString("hh:mm tt");
                    string end = TimeZoneInfo.ConvertTimeFromUtc(e.EndTime, tz).ToString("hh:mm tt");
                    sb.AppendLine($"  {name}: {durationMin} min ({start} – {end})");
                }
            }
            sb.AppendLine($"\nTotal Exercise Time: {totalMinutes} minutes");
            return sb.ToString();
        }

        [Description("Gets the full weekly workout plan history (Monday through Sunday) — shows what workout was planned for each day this week.")]
        public async Task<string> GetWeeklyWorkoutPlanHistory()
        {
            var history = await _storage.GetWeeklyHistoryAsync(_userId);
            if (history?.PastWorkoutPlans is not { Count: > 0 })
                return "No weekly workout plan history available.";

            var sb = new StringBuilder();
            sb.AppendLine($"=== This Week's Workout Plans (starting {history.WeekStartDate:MMM dd}) ===");
            var dayOrder = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
            foreach (var day in dayOrder)
            {
                if (history.PastWorkoutPlans.TryGetValue(day, out var plan))
                {
                    sb.AppendLine($"\n--- {day}: {plan.SessionTitle} ---");
                    if (plan.MainWorkout?.Count > 0)
                    {
                        foreach (var ex in plan.MainWorkout)
                            sb.AppendLine($"  {ex.Exercise}: {ex.Sets}x{ex.Reps} (Rest: {ex.Rest}) {ex.Notes}");
                    }
                    if (!string.IsNullOrEmpty(plan.CoachNotes))
                        sb.AppendLine($"  Coach: {plan.CoachNotes}");
                }
                else
                {
                    sb.AppendLine($"\n--- {day}: No plan ---");
                }
            }
            return sb.ToString();
        }

        [Description("Gets the full weekly diet plan history — shows what diet was planned for each day this week.")]
        public async Task<string> GetWeeklyDietPlanHistory()
        {
            var history = await _storage.GetWeeklyDietHistoryAsync(_userId);
            if (history?.PastDiets is not { Count: > 0 })
                return "No weekly diet plan history available.";

            var sb = new StringBuilder();
            sb.AppendLine($"=== This Week's Diet Plans (starting {history.WeekStartDate:MMM dd}) ===");
            var dayOrder = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
            foreach (var day in dayOrder)
            {
                if (history.PastDiets.TryGetValue(day, out var diet))
                {
                    sb.AppendLine($"\n--- {day} ({diet.TotalCaloriesTarget} kcal) ---");
                    foreach (var meal in diet.Meals)
                        sb.AppendLine($"  [{meal.MealType}] {meal.FoodName} - {meal.QuantityDescription} ({meal.Calories} cal)");
                }
                else
                {
                    sb.AppendLine($"\n--- {day}: No plan ---");
                }
            }
            return sb.ToString();
        }

        [Description("Gets the user's recent plan feedback — shows how they rated past workout and diet plans (rating, difficulty, skipped items, notes).")]
        public async Task<string> GetRecentFeedback()
        {
            var feedbacks = await _storage.GetRecentFeedbackAsync(_userId, 10);
            if (feedbacks.Count == 0) return "No plan feedback submitted yet.";

            var sb = new StringBuilder();
            sb.AppendLine("=== Recent Plan Feedback ===");
            foreach (var fb in feedbacks)
            {
                sb.AppendLine($"\n[{fb.FeedbackDate:MMM dd}] {fb.PlanType} — ⭐{fb.Rating}/5 ({fb.Difficulty})");
                if (fb.SkippedItems.Count > 0)
                    sb.AppendLine($"  Skipped: {string.Join(", ", fb.SkippedItems)}");
                if (!string.IsNullOrEmpty(fb.Note))
                    sb.AppendLine($"  Note: {fb.Note}");
            }
            return sb.ToString();
        }

        [Description("Gets the latest weekly behavioral digest — aggregated mood, water intake, workout completion percentage, recurring pains, consistent meals, and frequently skipped exercises.")]
        public async Task<string> GetWeeklyDigest()
        {
            // Try current week first, then last week
            var monday = DateTime.UtcNow.Date.AddDays(-(int)DateTime.UtcNow.DayOfWeek + (int)DayOfWeek.Monday);
            var digest = await _storage.GetWeeklyDigestAsync(_userId, monday.ToString("yyyy-MM-dd"));
            digest ??= await _storage.GetWeeklyDigestAsync(_userId, monday.AddDays(-7).ToString("yyyy-MM-dd"));

            if (digest is null) return "No weekly digest available yet.";

            var sb = new StringBuilder();
            sb.AppendLine($"=== Weekly Digest ({digest.WeekStart} to {digest.WeekEnd}) ===");
            sb.AppendLine($"Diary entries logged: {digest.DiaryDays} days");
            sb.AppendLine($"Average Mood: {digest.AvgMood:F1}/5 | Average Water: {digest.AvgWater:F1}L");
            sb.AppendLine($"Workout Completion: {digest.WorkoutCompletionPct:F0}% ({digest.TotalExercisesDone} done, {digest.TotalExercisesSkipped} skipped)");
            if (digest.RecurringPains.Count > 0)
                sb.AppendLine($"Recurring Pains: {string.Join(", ", digest.RecurringPains)}");
            if (digest.ConsistentMeals.Count > 0)
                sb.AppendLine($"Consistent Meals: {string.Join(", ", digest.ConsistentMeals)}");
            if (digest.FrequentlySkipped.Count > 0)
                sb.AppendLine($"Frequently Skipped: {string.Join(", ", digest.FrequentlySkipped)}");
            if (!string.IsNullOrEmpty(digest.DigestText))
                sb.AppendLine($"\nSummary: {digest.DigestText}");
            return sb.ToString();
        }

        [Description("Gets detailed InBody body composition analysis including segmental lean balance (left/right arms, legs, trunk), fat/muscle control targets, and metabolic health.")]
        public async Task<string> GetInBodyAnalysis()
        {
            var inbody = await _storage.GetLatestInBodyDataAsync(_userId);
            if (inbody is null) return "No InBody scan data available.";

            var sb = new StringBuilder();
            sb.AppendLine($"=== InBody Analysis (Scan: {inbody.ScanDate}) ===");
            sb.AppendLine($"Weight: {inbody.Core.WeightKg}kg | Body Fat: {inbody.Core.Pbf}% | SMM: {inbody.Core.SmmKg}kg | BMI: {inbody.Core.Bmi}");
            sb.AppendLine($"BMR: {inbody.Metabolism.Bmr} kcal | Visceral Fat Level: {inbody.Metabolism.VisceralFatLevel}");
            sb.AppendLine($"Fat Control: {inbody.Targets.FatControl}kg | Muscle Control: {inbody.Targets.MuscleControl}kg");
            sb.AppendLine();
            sb.AppendLine("=== Segmental Lean Analysis ===");
            sb.AppendLine($"Right Arm: {inbody.LeanBalance.RightArm}");
            sb.AppendLine($"Left Arm: {inbody.LeanBalance.LeftArm}");
            sb.AppendLine($"Trunk: {inbody.LeanBalance.Trunk}");
            sb.AppendLine($"Right Leg: {inbody.LeanBalance.RightLeg}");
            sb.AppendLine($"Left Leg: {inbody.LeanBalance.LeftLeg}");
            return sb.ToString();
        }

        [Description("Gets the user's recent in-app notifications (plan ready, health data received, errors, etc.).")]
        public Task<string> GetNotifications()
        {
            var notifs = _notifications.GetForUser(_userId);
            if (notifs.Count == 0) return Task.FromResult("No notifications.");

            var sb = new StringBuilder();
            sb.AppendLine($"=== Notifications ({_notifications.GetUnreadCount(_userId)} unread) ===");
            foreach (var n in notifs.Take(10))
            {
                string read = n.IsRead ? "" : " 🔴";
                sb.AppendLine($"[{n.CreatedAt:MMM dd HH:mm}] {n.Type}: {n.Title}{read}");
                sb.AppendLine($"  {n.Message}");
            }
            return Task.FromResult(sb.ToString());
        }

        [Description("Checks if a plan generation job is currently in progress or recently completed for the user.")]
        public Task<string> GetPlanGenerationStatus()
        {
            var job = _jobTracker.GetActiveJobForUser(_userId);
            if (job is null) return Task.FromResult("No active plan generation job. Plans are generated when new health data arrives via webhook.");

            var sb = new StringBuilder();
            sb.AppendLine($"=== Plan Generation Status ===");
            sb.AppendLine($"Job ID: {job.JobId}");
            sb.AppendLine($"Status: {job.Status} — {job.CurrentStep}");
            sb.AppendLine($"Queued: {job.QueuedAt:HH:mm}");
            if (job.CompletedAt.HasValue)
                sb.AppendLine($"Completed: {job.CompletedAt.Value:HH:mm}");
            if (!string.IsNullOrEmpty(job.ErrorMessage))
                sb.AppendLine($"Error: {job.ErrorMessage}");
            return Task.FromResult(sb.ToString());
        }

        private static TimeZoneInfo ResolveTimeZone()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(TimezoneHelper.CurrentTimezoneId); }
            catch { return TimeZoneInfo.Utc; }
        }

        private static string FormatDuration(int totalSeconds)
        {
            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            return hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m";
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

            // Auto-track onboarding steps
            if (!string.IsNullOrEmpty(excludedFoodsToAdd) || !string.IsNullOrEmpty(foodPreferences))
                MarkOnboardingStep(profile, "food");
            if (!string.IsNullOrEmpty(cuisineStyle))
                MarkOnboardingStep(profile, "cuisine");

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
            MarkOnboardingStep(profile, "conditions");
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

            MarkOnboardingStep(profile, "schedule");
            await _storage.SaveUserProfileAsync(_userId, profile);
            _logger.LogInformation("[ChatAgent] Updated workout schedule for {UserId}", _userId);
            return $"Workout schedule updated:\n{string.Join("\n", changes)}";
        }

        [Description("Adds or updates a diary entry for a given date. Merges with existing data — does not replace. MUST call GetTodayDiary or GetRecentDiaryHistory first to see what's already logged. Parameters: date (yyyy-MM-dd, defaults to today if omitted), mealsJson (JSON array of {mealTime, foodName, quantity, wasFromPlan, substitution}), workoutLogJson (JSON array of {exercise, completed, feeling, notes}), painLogJson (JSON array of {bodyArea, severity, description}), moodEnergy (1-5), waterIntakeLitres (double), sleepNotes (string), generalNotes (string). All parameters optional — provide only what needs updating.")]
        public async Task<string> UpsertDiaryEntry(
            string? date = null,
            string? mealsJson = null,
            string? workoutLogJson = null,
            string? painLogJson = null,
            int? moodEnergy = null,
            double? waterIntakeLitres = null,
            string? sleepNotes = null,
            string? generalNotes = null)
        {
            string targetDate = !string.IsNullOrWhiteSpace(date) && DateTime.TryParseExact(date, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out _)
                ? date
                : DateTime.UtcNow.ToString("yyyy-MM-dd");
            var existing = await _storage.GetDiaryEntryAsync(_userId, targetDate) ?? new DailyDiary { Date = targetDate };
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
            _logger.LogInformation("[ChatAgent] Updated diary for {UserId} on {Date}: {Changes}", _userId, targetDate, string.Join("; ", changes));
            return $"Diary updated for {targetDate}:\n{string.Join("\n", changes)}";
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

        [Description("Updates the user's personal information (name, age, notification time). Merges — only updates fields that are provided. MUST call GetUserProfile first. Parameters: firstName (optional), lastName (optional), age (optional, integer), notificationTime (optional, HH:mm 24-hour format).")]
        public async Task<string> UpdatePersonalInfo(
            string? firstName = null,
            string? lastName = null,
            int? age = null,
            string? notificationTime = null)
        {
            var profile = await _storage.GetUserProfileAsync(_userId);
            if (profile is null) return "Error: User profile not found.";

            var changes = new List<string>();

            if (!string.IsNullOrEmpty(firstName))
            {
                profile.FirstName = firstName;
                changes.Add($"First name set to: {firstName}");
            }

            if (!string.IsNullOrEmpty(lastName))
            {
                profile.LastName = lastName;
                changes.Add($"Last name set to: {lastName}");
            }

            if (age.HasValue)
            {
                profile.Age = Math.Clamp(age.Value, 10, 120);
                changes.Add($"Age set to: {profile.Age}");
            }

            if (!string.IsNullOrEmpty(notificationTime))
            {
                profile.NotificationTime = notificationTime;
                changes.Add($"Notification time set to: {notificationTime}");
            }

            if (changes.Count == 0) return "No personal info changes specified.";

            // Auto-track onboarding steps
            if (!string.IsNullOrEmpty(firstName) || !string.IsNullOrEmpty(lastName))
                MarkOnboardingStep(profile, "name");
            if (age.HasValue)
                MarkOnboardingStep(profile, "age");

            await _storage.SaveUserProfileAsync(_userId, profile);
            _logger.LogInformation("[ChatAgent] Updated personal info for {UserId}: {Changes}", _userId, string.Join("; ", changes));
            return $"Profile updated:\n{string.Join("\n", changes)}";
        }

        /// <summary>
        /// Auto-tracks an onboarding step when the corresponding write tool executes.
        /// Also evaluates overall onboarding completion (name + age + conditions = minimum).
        /// </summary>
        private static void MarkOnboardingStep(UserProfile profile, string step)
        {
            if (!profile.OnboardingCompleted.Contains(step, StringComparer.OrdinalIgnoreCase))
                profile.OnboardingCompleted.Add(step.ToLowerInvariant());

            var minimumSteps = new[] { "name", "age", "conditions" };
            profile.IsOnboardingComplete = minimumSteps.All(s =>
                profile.OnboardingCompleted.Contains(s, StringComparer.OrdinalIgnoreCase));
        }

        [Description("Gets the user's onboarding status — which steps are completed and which are remaining.")]
        public async Task<string> GetOnboardingStatus()
        {
            var profile = await _storage.GetUserProfileAsync(_userId);
            if (profile is null) return "No profile found. Onboarding has not started.";

            var allSteps = new[] { "name", "age", "conditions", "food", "cuisine", "schedule" };
            var completed = profile.OnboardingCompleted;
            var remaining = allSteps.Where(s => !completed.Contains(s, StringComparer.OrdinalIgnoreCase)).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"Onboarding complete: {profile.IsOnboardingComplete}");
            sb.AppendLine($"Completed steps: {(completed.Count > 0 ? string.Join(", ", completed) : "None")}");
            sb.AppendLine($"Remaining steps: {(remaining.Count > 0 ? string.Join(", ", remaining) : "All done!")}");
            return sb.ToString();
        }

        [Description("Gets a list of frequently eaten foods from the user's recent diary entries (last 14 days). Useful for suggesting meals during diary logging or chat-based meal tracking.")]
        public async Task<string> GetFrequentMeals()
        {
            var entries = await _storage.GetRecentDiaryEntriesAsync(_userId, 14);
            if (entries.Count == 0) return "No diary entries found. No meal history available yet.";

            var mealCounts = entries
                .SelectMany(e => e.ActualMeals)
                .Where(m => !string.IsNullOrWhiteSpace(m.FoodName))
                .GroupBy(m => m.FoodName.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Take(15)
                .ToList();

            if (mealCounts.Count == 0) return "No meals logged in last 14 days.";

            var sb = new StringBuilder();
            sb.AppendLine("=== Frequently Eaten Foods (last 14 days) ===");
            foreach (var group in mealCounts)
            {
                var typicalQty = group.First().Quantity;
                var typicalTime = group.First().MealTime;
                sb.AppendLine($"  {group.Key} — {group.Count()}x (typical: {typicalQty}, {typicalTime})");
            }
            return sb.ToString();
        }
    }
}
