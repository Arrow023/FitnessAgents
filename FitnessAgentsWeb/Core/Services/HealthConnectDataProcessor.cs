using FitnessAgentsWeb.Core.Helpers;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models;
using FitnessAgentsWeb.Tools;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Core.Services
{
    public class HealthConnectDataProcessor : IHealthDataProcessor
    {
        private readonly IStorageRepository _storageRepository;
        private readonly IConfiguration _configuration;
        private readonly TimeZoneInfo _istZone;
        private readonly Microsoft.Extensions.Logging.ILogger<HealthConnectDataProcessor> _logger;

        public HealthConnectDataProcessor(IStorageRepository storageRepository, IConfiguration configuration, Microsoft.Extensions.Logging.ILogger<HealthConnectDataProcessor> logger)
        {
            _storageRepository = storageRepository;
            _configuration = configuration;
            _logger = logger;
            
            try { _istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
            catch
            {
                try { _istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
                catch { _istZone = TimeZoneInfo.Local; }
            }
        }

        public async Task<HealthExportPayload> ProcessAndMergeHealthDataAsync(string userId, HealthExportPayload newPayload)
        {
            var existingPayload = await _storageRepository.GetTodayHealthDataAsync(userId);

            if (existingPayload == null)
            {
                return newPayload;
            }

            DateTime cutoff = DateTime.UtcNow.AddDays(-15);

            return new HealthExportPayload
            {
                Sleep = existingPayload.Sleep.Concat(newPayload.Sleep)
                    .GroupBy(s => s.SessionEndTime).Select(g => g.First())
                    .Where(s => s.SessionEndTime >= cutoff).ToList(),

                Steps = existingPayload.Steps.Concat(newPayload.Steps)
                    .GroupBy(s => s.EndTime).Select(g => g.First())
                    .Where(s => s.EndTime >= cutoff).ToList(),

                HeartRate = existingPayload.HeartRate.Concat(newPayload.HeartRate)
                    .GroupBy(r => r.Time).Select(g => g.First())
                    .Where(r => r.Time >= cutoff).ToList(),

                RestingHeartRate = existingPayload.RestingHeartRate.Concat(newPayload.RestingHeartRate)
                    .GroupBy(r => r.Time).Select(g => g.First())
                    .Where(r => r.Time >= cutoff).ToList(),

                HRV = existingPayload.HRV.Concat(newPayload.HRV)
                    .GroupBy(h => h.Time).Select(g => g.First())
                    .Where(h => h.Time >= cutoff).ToList(),

                ActiveCalories = existingPayload.ActiveCalories.Concat(newPayload.ActiveCalories)
                    .GroupBy(c => c.EndTime).Select(g => g.First())
                    .Where(c => c.EndTime >= cutoff).ToList(),

                TotalCalories = existingPayload.TotalCalories.Concat(newPayload.TotalCalories)
                    .GroupBy(c => c.EndTime).Select(g => g.First())
                    .Where(c => c.EndTime >= cutoff).ToList(),

                Distance = existingPayload.Distance.Concat(newPayload.Distance)
                    .GroupBy(d => d.EndTime).Select(g => g.First())
                    .Where(d => d.EndTime >= cutoff).ToList(),

                Exercise = existingPayload.Exercise.Concat(newPayload.Exercise)
                    .GroupBy(e => e.StartTime).Select(g => g.First())
                    .Where(e => e.StartTime >= cutoff).ToList(),

                BloodPressure = existingPayload.BloodPressure.Concat(newPayload.BloodPressure)
                    .GroupBy(b => b.Time).Select(g => g.First())
                    .Where(b => b.Time >= cutoff).ToList(),

                BloodGlucose = existingPayload.BloodGlucose.Concat(newPayload.BloodGlucose)
                    .GroupBy(b => b.Time).Select(g => g.First())
                    .Where(b => b.Time >= cutoff).ToList(),

                OxygenSaturation = existingPayload.OxygenSaturation.Concat(newPayload.OxygenSaturation)
                    .GroupBy(o => o.Time).Select(g => g.First())
                    .Where(o => o.Time >= cutoff).ToList(),

                BodyTemperature = existingPayload.BodyTemperature.Concat(newPayload.BodyTemperature)
                    .GroupBy(t => t.Time).Select(g => g.First())
                    .Where(t => t.Time >= cutoff).ToList(),

                RespiratoryRate = existingPayload.RespiratoryRate.Concat(newPayload.RespiratoryRate)
                    .GroupBy(r => r.Time).Select(g => g.First())
                    .Where(r => r.Time >= cutoff).ToList(),

                Hydration = existingPayload.Hydration.Concat(newPayload.Hydration)
                    .GroupBy(h => h.EndTime).Select(g => g.First())
                    .Where(h => h.EndTime >= cutoff).ToList(),

                Nutrition = existingPayload.Nutrition.Concat(newPayload.Nutrition)
                    .GroupBy(n => n.EndTime).Select(g => g.First())
                    .Where(n => n.EndTime >= cutoff).ToList(),

                // Keep all recent VO2max records + always retain the latest one (VO2 tests are infrequent)
                Vo2Max = existingPayload.Vo2Max.Concat(newPayload.Vo2Max)
                    .GroupBy(v => v.Time).Select(g => g.First())
                    .OrderByDescending(v => v.Time)
                    .Where((v, i) => v.Time >= cutoff || i == 0)
                    .ToList()
            };
        }

        public async Task<UserHealthContext> LoadHealthStateToRAMAsync(string userId, HealthExportPayload hc)
        {
            var context = new UserHealthContext { UserId = userId };

            if (hc != null)
            {
                var allDates = hc.Sleep.Select(s => s.SessionEndTime)
                    .Concat(hc.Steps.Select(s => s.EndTime))
                    .Concat(hc.ActiveCalories.Select(c => c.EndTime))
                    .Concat(hc.HRV.Select(h => h.Time))
                    .ToList();

                DateTime latestDataUtc = allDates.Any() ? allDates.Max() : DateTime.UtcNow;
                DateTime targetDateIst = TimeZoneInfo.ConvertTimeFromUtc(latestDataUtc, _istZone).Date;

                context.DataTimestamp = TimeZoneInfo.ConvertTimeFromUtc(latestDataUtc, _istZone).ToString("MMM dd, hh:mm tt");

                DateTime activityStart = targetDateIst;
                DateTime activityEnd = targetDateIst.AddDays(1);
                DateTime sleepStart = targetDateIst.AddHours(-12);
                DateTime sleepEnd = targetDateIst.AddHours(12);

                bool IsTargetActivity(DateTime utcTime) => TimeZoneInfo.ConvertTimeFromUtc(utcTime, _istZone) >= activityStart && TimeZoneInfo.ConvertTimeFromUtc(utcTime, _istZone) < activityEnd;
                bool IsTargetSleep(DateTime utcTime) => TimeZoneInfo.ConvertTimeFromUtc(utcTime, _istZone) >= sleepStart && TimeZoneInfo.ConvertTimeFromUtc(utcTime, _istZone) < sleepEnd;

                // ── Sleep metrics ──
                var targetSleepSessions = hc.Sleep.Where(s => IsTargetSleep(s.SessionEndTime)).ToList();
                int totalSleepSecs = targetSleepSessions.SelectMany(s => s.Stages).Where(st => st.Stage != "1" && st.Stage != "2").Sum(st => st.DurationSeconds);
                context.VitalsSleepTotal = $"{totalSleepSecs / 3600}h {(totalSleepSecs % 3600) / 60}m";

                int deepSleepSecs = targetSleepSessions.SelectMany(s => s.Stages).Where(st => st.Stage == "5").Sum(st => st.DurationSeconds);
                context.VitalsSleepDeep = $"{deepSleepSecs / 3600}h {(deepSleepSecs % 3600) / 60}m";

                // Sleep efficiency: actual sleep / total time in bed
                int totalTimeInBedSecs = targetSleepSessions.Sum(s => s.DurationSeconds);
                if (totalTimeInBedSecs > 0)
                {
                    double efficiency = (double)totalSleepSecs / totalTimeInBedSecs * 100;
                    context.SleepEfficiency = Math.Round(efficiency, 0).ToString("0");
                }

                // ── Heart metrics ──
                var targetHrv = hc.HRV.Where(h => IsTargetActivity(h.Time)).OrderByDescending(h => h.Time).FirstOrDefault();
                context.VitalsHrv = targetHrv != null ? Math.Round(targetHrv.Rmssd, 0).ToString() : "--";

                var targetRhr = hc.RestingHeartRate.Where(r => IsTargetActivity(r.Time)).OrderByDescending(r => r.Time).FirstOrDefault();
                context.VitalsRhr = targetRhr != null ? targetRhr.Bpm.ToString() : "--";

                // ── Activity metrics ──
                var latestSteps = hc.Steps.Where(s => IsTargetActivity(s.EndTime)).OrderByDescending(s => s.EndTime).FirstOrDefault();
                context.VitalsSteps = latestSteps != null ? latestSteps.Count.ToString("N0") : "0";

                var latestDist = hc.Distance.Where(d => IsTargetActivity(d.EndTime)).OrderByDescending(d => d.EndTime).FirstOrDefault();
                context.VitalsDistance = latestDist != null ? (latestDist.Meters / 1000.0).ToString("0.00") + " km" : "0.00 km";

                var latestActiveCals = hc.ActiveCalories.Where(c => IsTargetActivity(c.EndTime)).OrderByDescending(c => c.EndTime).FirstOrDefault();
                var latestTotalCals = hc.TotalCalories.Where(c => IsTargetActivity(c.EndTime)).OrderByDescending(c => c.EndTime).FirstOrDefault();

                context.VitalsCalories = latestActiveCals != null ? Math.Round(latestActiveCals.Calories, 0).ToString("N0") + " kcal" : "0 kcal";
                context.VitalsTotalCalories = latestTotalCals != null ? Math.Round(latestTotalCals.Calories, 0).ToString("N0") + " kcal" : "0 kcal";

                // ── Phase 2: Expanded Vitals ──
                var latestBp = hc.BloodPressure.Where(b => IsTargetActivity(b.Time)).OrderByDescending(b => b.Time).FirstOrDefault();
                if (latestBp != null)
                    context.VitalsBloodPressure = $"{Math.Round(latestBp.Systolic, 0)}/{Math.Round(latestBp.Diastolic, 0)}";

                var latestSpO2 = hc.OxygenSaturation.Where(o => IsTargetActivity(o.Time)).OrderByDescending(o => o.Time).FirstOrDefault();
                if (latestSpO2 != null)
                    context.VitalsSpO2 = Math.Round(latestSpO2.Percentage, 0).ToString();

                // VO2max from Health Connect
                var latestVo2 = hc.Vo2Max.Where(v => IsTargetActivity(v.Time)).OrderByDescending(v => v.Time).FirstOrDefault();
                if (latestVo2 != null)
                    context.VitalsVo2Max = Math.Round(latestVo2.Vo2MlPerMinKg, 1).ToString("0.0");

                var latestRespRate = hc.RespiratoryRate.Where(r => IsTargetActivity(r.Time)).OrderByDescending(r => r.Time).FirstOrDefault();
                if (latestRespRate != null)
                    context.VitalsRespiratoryRate = Math.Round(latestRespRate.Rate, 0).ToString();

                double totalHydration = hc.Hydration.Where(h => IsTargetActivity(h.EndTime)).Sum(h => h.Liters);
                if (totalHydration > 0)
                    context.VitalsHydration = totalHydration.ToString("0.0");

                // Nutrition (daily aggregate)
                var todayNutrition = hc.Nutrition.Where(n => IsTargetActivity(n.EndTime)).ToList();
                if (todayNutrition.Any())
                {
                    context.VitalsNutritionCalories = Math.Round(todayNutrition.Sum(n => n.Calories ?? 0), 0).ToString("N0");
                    context.VitalsProtein = Math.Round(todayNutrition.Sum(n => n.ProteinGrams ?? 0), 0).ToString();
                    context.VitalsCarbs = Math.Round(todayNutrition.Sum(n => n.CarbsGrams ?? 0), 0).ToString();
                    context.VitalsFat = Math.Round(todayNutrition.Sum(n => n.FatGrams ?? 0), 0).ToString();
                }

                // ── Phase 3: Computed Insights ──

                // Calorie balance (intake - expenditure)
                double nutritionCals = todayNutrition.Sum(n => n.Calories ?? 0);
                double burnedCals = latestTotalCals?.Calories ?? 0;
                if (nutritionCals > 0 || burnedCals > 0)
                {
                    double balance = nutritionCals - burnedCals;
                    context.CalorieBalance = (balance >= 0 ? "+" : "") + Math.Round(balance, 0).ToString("N0");
                }

                // Exercise log (today's sessions as JSON for view rendering)
                var todayExercise = hc.Exercise.Where(e => IsTargetActivity(e.StartTime))
                    .OrderByDescending(e => e.StartTime).ToList();
                if (todayExercise.Any())
                {
                    var exerciseEntries = todayExercise.Select(e => new
                    {
                        type = ExerciseTypeHelper.GetExerciseName(e.Type),
                        icon = ExerciseTypeHelper.GetExerciseIcon(e.Type),
                        duration = e.DurationSeconds / 60,
                        time = TimeZoneInfo.ConvertTimeFromUtc(e.StartTime, _istZone).ToString("hh:mm tt")
                    });
                    context.ExerciseLog = JsonSerializer.Serialize(exerciseEntries);
                }

                // Active minutes this week (all exercise within 7 days)
                DateTime weekStart = targetDateIst.AddDays(-6);
                int activeMinutes = hc.Exercise
                    .Where(e => TimeZoneInfo.ConvertTimeFromUtc(e.StartTime, _istZone).Date >= weekStart)
                    .Sum(e => e.DurationSeconds) / 60;
                context.ActiveMinutesWeekly = activeMinutes.ToString();

                // Recovery score (0–100 composite: HRV, RHR, sleep quality, SpO2)
                context.RecoveryScore = ComputeRecoveryScore(context, latestSpO2);

                // Sleep score (0–100 composite: duration, deep sleep, efficiency)
                context.SleepScore = ComputeSleepScore(totalSleepSecs, deepSleepSecs, totalTimeInBedSecs);

                // Active score (0–100 composite: steps, calories, exercise minutes)
                int todayExerciseMinutes = todayExercise.Sum(e => e.DurationSeconds) / 60;
                context.ActiveScore = ComputeActiveScore(
                    latestSteps?.Count ?? 0,
                    latestActiveCals?.Calories ?? 0,
                    todayExerciseMinutes);

                // ── 7-day Trend Data (sparklines) ──
                ComputeTrends(hc, context, targetDateIst);

                // ── 15-day Averages ──
                Compute15DayAverages(hc, context, targetDateIst);

                context.ReadinessBrief = $"[TARGET DAY: {targetDateIst:MMM dd}] Sleep: {context.VitalsSleepTotal} (Deep: {context.VitalsSleepDeep}, Sleep Score: {context.SleepScore}/100). RHR: {context.VitalsRhr} bpm. HRV: {context.VitalsHrv}. Steps: {context.VitalsSteps}. Active Burn: {context.VitalsCalories} (Total: {Math.Round(latestTotalCals?.Calories ?? 0, 0)} kcal). Recovery: {context.RecoveryScore}/100. Active Score: {context.ActiveScore}/100. VO2max: {context.VitalsVo2Max}. 15-day Avg RHR: {context.AvgRhr15Day}, HRV: {context.AvgHrv15Day}, Steps: {context.AvgSteps15Day}, Sleep: {context.AvgSleep15Day}.";
            }

            // Load Weekly History Brief
            var weeklyHistory = await _storageRepository.GetWeeklyHistoryAsync(userId);
            if (weeklyHistory != null && weeklyHistory.PastWorkouts.Any())
            {
                var summaries = weeklyHistory.PastWorkouts.Select(kvp =>
                {
                    // Extract only the title (first meaningful line starting with #) or first line
                    var lines = kvp.Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    var title = lines.FirstOrDefault(l => l.TrimStart().StartsWith('#'))
                                ?? lines.FirstOrDefault()
                                ?? kvp.Value;
                    // Strip markdown heading markers
                    title = title.TrimStart('#', ' ');
                    // Cap at 80 chars
                    if (title.Length > 80) title = title[..80] + "...";
                    return $"{kvp.Key}: {title}";
                });
                context.WeeklyHistoryBrief = "This week's completed workouts:\n" + string.Join("\n", summaries);
            }
            else
            {
                context.WeeklyHistoryBrief = "It's the start of a new week. No workouts completed yet.";
            }

            // Load Weekly Diet History
            var weeklyDiet = await _storageRepository.GetWeeklyDietHistoryAsync(userId);
            if (weeklyDiet != null && weeklyDiet.PastDiets.Any())
            {
                var summaries = weeklyDiet.PastDiets.Select(kvp => 
                    $"{kvp.Key}: {kvp.Value.TotalCaloriesTarget} kcal - {kvp.Value.AiSummary}");
                context.DietHistoryBrief = "This week's consumed diets:\n" + string.Join("\n", summaries);
            }
            else
            {
                context.DietHistoryBrief = "No previous diet history for this week. This is the first professional diet plan.";
            }

            // Load User Profile (FirstName & Preferences)
            _logger.LogInformation($"[HealthProcessor] Loading profile for: {userId} (normalized if needed by repo)");
            var profile = await _storageRepository.GetUserProfileAsync(userId);
            if (profile != null)
            {
                _logger.LogInformation($"[HealthProcessor] Profile FOUND. FirstName: {profile.FirstName}, Email: {profile.Email}, Prefs: {profile.Preferences}");
                context.FirstName = string.IsNullOrEmpty(profile.FirstName) ? userId : profile.FirstName;
                context.Email = profile.Email;
                if (!string.IsNullOrEmpty(profile.Preferences))
                {
                    context.ConditionsBrief = profile.Preferences;
                }
                if (!string.IsNullOrEmpty(profile.FoodPreferences))
                {
                    context.FoodPreferences = profile.FoodPreferences;
                }
                if (profile.ExcludedFoods?.Count > 0)
                    context.ExcludedFoods = profile.ExcludedFoods;
                if (!string.IsNullOrEmpty(profile.CuisineStyle))
                    context.CuisineStyle = profile.CuisineStyle;
                if (profile.CookingOils?.Count > 0)
                    context.CookingOils = profile.CookingOils;
                if (profile.StapleGrains?.Count > 0)
                    context.StapleGrains = profile.StapleGrains;
                if (profile.WorkoutSchedule != null && profile.WorkoutSchedule.Any())
                {
                    context.WorkoutSchedule = profile.WorkoutSchedule;
                }
            }
            else
            {
                _logger.LogWarning($"[HealthProcessor] Profile NOT FOUND for {userId}");
            }

            // Load recent diary entries to build a diary brief for AI context
            try
            {
                var diaryEntries = await _storageRepository.GetRecentDiaryEntriesAsync(userId, 7);
                if (diaryEntries.Count > 0)
                {
                    context.DiaryBrief = BuildDiaryBrief(diaryEntries);
                    _logger.LogInformation("[HealthProcessor] Built diary brief from {Count} entries", diaryEntries.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[HealthProcessor] Failed loading diary entries");
            }

            // Estimate VO2max from RHR if not available from Health Connect
            if (context.VitalsVo2Max == "--" && int.TryParse(context.VitalsRhr, out int rhrForVo2) && rhrForVo2 > 0)
            {
                int age = profile?.Age ?? 30;
                double hrMax = 220.0 - age;
                double estimatedVo2 = 15.3 * (hrMax / rhrForVo2);
                context.VitalsVo2Max = Math.Round(estimatedVo2, 1).ToString("0.0") + "~";
            }

            // Load User-Specific InBody & Conditions from Firebase
            try
            {
                var scan = await _storageRepository.GetLatestInBodyDataAsync(userId);
                if (scan != null)
                {
                    context.InBodyWeight = scan.Core.WeightKg.ToString("0.0");
                    context.InBodyBf = scan.Core.Pbf.ToString("0.0");
                    context.InBodySmm = scan.Core.SmmKg.ToString("0.0");
                    context.InBodyBmr = scan.Metabolism.Bmr.ToString();
                    context.InBodyVisceral = scan.Metabolism.VisceralFatLevel.ToString();
                    context.InBodyBmi = scan.Core.Bmi.ToString("0.0");
                    context.InBodyScanDate = scan.ScanDate ?? "--";
                    context.InBodyFatControl = scan.Targets.FatControl.ToString("0.0");
                    context.InBodyMuscleControl = scan.Targets.MuscleControl.ToString("0.0");

                    var imbalances = new List<string>();
                    if (scan.LeanBalance.LeftLeg is "Under" or "Over" || scan.LeanBalance.RightLeg is "Under" or "Over") imbalances.Add("Legs");
                    if (scan.LeanBalance.LeftArm is "Under" or "Over" || scan.LeanBalance.RightArm is "Under" or "Over") imbalances.Add("Arms");
                    if (scan.LeanBalance.Trunk is "Under" or "Over") imbalances.Add("Core");
                    context.InBodyImbalances = imbalances.Any() ? string.Join(", ", imbalances) : "Balanced";

                    context.InBodyBrief = $"Weight: {context.InBodyWeight}kg. Body Fat: {context.InBodyBf}%. BMR: {context.InBodyBmr} kcal. SMM: {context.InBodySmm}kg VisceralFat: {context.InBodyVisceral} BMI: {context.InBodyBmi} Focus: {context.InBodyImbalances}.";
                }
                else
                {
                    // Fallback to Gist if no user data exists (Legacy/Initial)
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                    string inBodyUrl = _configuration["ExternalData:InBodyUrl"];
                    if (!string.IsNullOrEmpty(inBodyUrl))
                    {
                        var gistScan = JsonSerializer.Deserialize<InBodyExport>(await client.GetStringAsync(inBodyUrl));
                        if (gistScan != null)
                        {
                            context.InBodyWeight = gistScan.Core.WeightKg.ToString("0.1");
                            context.InBodyBf = gistScan.Core.Pbf.ToString("0.1");
                            context.InBodyBrief = $"[Gist Data] Weight: {context.InBodyWeight}kg. Body Fat: {context.InBodyBf}%.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[Error Loading InBody for {userId}]");
            }

            return context;
        }

        private static int ComputeRecoveryScore(UserHealthContext ctx, OxygenSaturationRecord? spO2)
        {
            // Weighted composite: HRV (35%), RHR (25%), Sleep (25%), SpO2 (15%)
            double score = 0;
            int factors = 0;

            if (double.TryParse(ctx.VitalsHrv, out double hrv) && hrv > 0)
            {
                // HRV: 20ms = poor (0), 60ms = good (100)
                double hrvScore = Math.Clamp((hrv - 20) / 40 * 100, 0, 100);
                score += hrvScore * 0.35;
                factors++;
            }

            if (int.TryParse(ctx.VitalsRhr, out int rhr) && rhr > 0)
            {
                // RHR: 80bpm = poor (0), 50bpm = excellent (100)
                double rhrScore = Math.Clamp((80 - rhr) / 30.0 * 100, 0, 100);
                score += rhrScore * 0.25;
                factors++;
            }

            if (double.TryParse(ctx.SleepEfficiency, out double sleepEff) && sleepEff > 0)
            {
                // Sleep efficiency: 60% = poor (0), 95% = excellent (100)
                double sleepScore = Math.Clamp((sleepEff - 60) / 35 * 100, 0, 100);
                score += sleepScore * 0.25;
                factors++;
            }

            if (spO2 != null && spO2.Percentage > 0)
            {
                // SpO2: 90% = poor (0), 99% = excellent (100)
                double spo2Score = Math.Clamp((spO2.Percentage - 90) / 9 * 100, 0, 100);
                score += spo2Score * 0.15;
                factors++;
            }

            if (factors == 0) return 0;

            // Normalize if not all factors present
            double totalWeight = (factors >= 1 ? 0.35 : 0) + (factors >= 2 ? 0.25 : 0) + (factors >= 3 ? 0.25 : 0) + (factors >= 4 ? 0.15 : 0);
            // Re-compute with actual available weights
            return (int)Math.Round(Math.Clamp(score / totalWeight * 1.0, 0, 100));
        }

        private void ComputeTrends(HealthExportPayload hc, UserHealthContext ctx, DateTime targetDateIst)
        {
            var days = Enumerable.Range(0, 7).Select(i => targetDateIst.AddDays(-6 + i)).ToList();

            // RHR trend
            var rhrPoints = days.Select(day =>
            {
                var match = hc.RestingHeartRate
                    .Where(r => TimeZoneInfo.ConvertTimeFromUtc(r.Time, _istZone).Date == day)
                    .OrderByDescending(r => r.Time).FirstOrDefault();
                return match?.Bpm ?? 0;
            }).ToList();
            ctx.RhrTrend = JsonSerializer.Serialize(rhrPoints);

            // HRV trend
            var hrvPoints = days.Select(day =>
            {
                var match = hc.HRV
                    .Where(h => TimeZoneInfo.ConvertTimeFromUtc(h.Time, _istZone).Date == day)
                    .OrderByDescending(h => h.Time).FirstOrDefault();
                return match != null ? (int)Math.Round(match.Rmssd) : 0;
            }).ToList();
            ctx.HrvTrend = JsonSerializer.Serialize(hrvPoints);

            // Steps trend
            var stepsPoints = days.Select(day =>
            {
                var match = hc.Steps
                    .Where(s => TimeZoneInfo.ConvertTimeFromUtc(s.EndTime, _istZone).Date == day)
                    .OrderByDescending(s => s.EndTime).FirstOrDefault();
                return match?.Count ?? 0;
            }).ToList();
            ctx.StepsTrend = JsonSerializer.Serialize(stepsPoints);

            // Sleep trend (hours)
            var sleepPoints = days.Select(day =>
            {
                var sleepStart = day.AddHours(-12);
                var sleepEnd = day.AddHours(12);
                var sessions = hc.Sleep.Where(s =>
                {
                    var ist = TimeZoneInfo.ConvertTimeFromUtc(s.SessionEndTime, _istZone);
                    return ist >= sleepStart && ist < sleepEnd;
                }).ToList();
                int secs = sessions.SelectMany(s => s.Stages)
                    .Where(st => st.Stage != "1" && st.Stage != "2")
                    .Sum(st => st.DurationSeconds);
                return Math.Round(secs / 3600.0, 1);
            }).ToList();
            ctx.SleepTrend = JsonSerializer.Serialize(sleepPoints);
        }

        private static int ComputeSleepScore(int totalSleepSecs, int deepSleepSecs, int totalTimeInBedSecs)
        {
            if (totalSleepSecs <= 0) return 0;

            // Duration (40%): <5h (18000s) = 0, 8h (28800s) = 100
            double durationScore = Math.Clamp((totalSleepSecs - 18000) / 10800.0 * 100, 0, 100);

            // Deep sleep (30%): 0s = 0, ≥1.5h (5400s) = 100
            double deepScore = Math.Clamp(deepSleepSecs / 5400.0 * 100, 0, 100);

            // Efficiency (30%): 60% = 0, 95% = 100
            double efficiencyScore = 0;
            if (totalTimeInBedSecs > 0)
            {
                double eff = (double)totalSleepSecs / totalTimeInBedSecs * 100;
                efficiencyScore = Math.Clamp((eff - 60) / 35 * 100, 0, 100);
            }

            return (int)Math.Round(durationScore * 0.40 + deepScore * 0.30 + efficiencyScore * 0.30);
        }

        private static int ComputeActiveScore(int steps, double activeCalories, int exerciseMinutes)
        {
            // Steps (40%): Target 10,000
            double stepsScore = Math.Clamp(steps / 10000.0 * 100, 0, 100);

            // Active calories (30%): Target 500 kcal
            double calScore = Math.Clamp(activeCalories / 500.0 * 100, 0, 100);

            // Exercise minutes (30%): Target 30 min/day
            double exerciseScore = Math.Clamp(exerciseMinutes / 30.0 * 100, 0, 100);

            return (int)Math.Round(stepsScore * 0.40 + calScore * 0.30 + exerciseScore * 0.30);
        }

        private void Compute15DayAverages(HealthExportPayload hc, UserHealthContext ctx, DateTime targetDateIst)
        {
            var days = Enumerable.Range(0, 15).Select(i => targetDateIst.AddDays(-14 + i)).ToList();

            // 15-day average RHR
            var rhrValues = days.Select(day => hc.RestingHeartRate
                .Where(r => TimeZoneInfo.ConvertTimeFromUtc(r.Time, _istZone).Date == day)
                .OrderByDescending(r => r.Time).FirstOrDefault()?.Bpm ?? 0)
                .Where(v => v > 0).ToList();
            if (rhrValues.Any())
                ctx.AvgRhr15Day = Math.Round(rhrValues.Average()).ToString("0");

            // 15-day average HRV
            var hrvValues = days.Select(day => hc.HRV
                .Where(h => TimeZoneInfo.ConvertTimeFromUtc(h.Time, _istZone).Date == day)
                .OrderByDescending(h => h.Time).FirstOrDefault())
                .Where(h => h != null).Select(h => h!.Rmssd).ToList();
            if (hrvValues.Any())
                ctx.AvgHrv15Day = Math.Round(hrvValues.Average()).ToString("0");

            // 15-day average Steps
            var stepsValues = days.Select(day => hc.Steps
                .Where(s => TimeZoneInfo.ConvertTimeFromUtc(s.EndTime, _istZone).Date == day)
                .OrderByDescending(s => s.EndTime).FirstOrDefault()?.Count ?? 0)
                .Where(v => v > 0).ToList();
            if (stepsValues.Any())
                ctx.AvgSteps15Day = Math.Round(stepsValues.Average()).ToString("N0");

            // 15-day average Sleep (hours)
            var sleepValues = days.Select(day =>
            {
                var sleepStart = day.AddHours(-12);
                var sleepEnd = day.AddHours(12);
                var sessions = hc.Sleep.Where(s =>
                {
                    var ist = TimeZoneInfo.ConvertTimeFromUtc(s.SessionEndTime, _istZone);
                    return ist >= sleepStart && ist < sleepEnd;
                }).ToList();
                int secs = sessions.SelectMany(s => s.Stages)
                    .Where(st => st.Stage != "1" && st.Stage != "2")
                    .Sum(st => st.DurationSeconds);
                return Math.Round(secs / 3600.0, 1);
            }).Where(v => v > 0).ToList();
            if (sleepValues.Any())
                ctx.AvgSleep15Day = Math.Round(sleepValues.Average(), 1).ToString("0.0") + "h";
        }

        private static string BuildDiaryBrief(List<DailyDiary> entries)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var entry in entries.Take(5))
            {
                sb.Append($"[{entry.Date}] ");
                if (entry.ActualMeals.Count > 0)
                {
                    var foods = entry.ActualMeals.Select(m => $"{m.MealTime}: {m.FoodName}");
                    sb.Append($"Ate: {string.Join(", ", foods)}. ");
                }
                if (entry.WorkoutLog.Count > 0)
                {
                    int done = entry.WorkoutLog.Count(w => w.Completed);
                    int skipped = entry.WorkoutLog.Count(w => w.Feeling == "skipped");
                    sb.Append($"Workout: {done} done, {skipped} skipped. ");
                }
                if (entry.PainLog.Count > 0)
                {
                    var pains = entry.PainLog.Select(p => $"{p.BodyArea} ({p.Severity}/5)");
                    sb.Append($"Pain: {string.Join(", ", pains)}. ");
                }
                if (entry.MoodEnergy > 0)
                    sb.Append($"Mood: {entry.MoodEnergy}/5. ");
                if (entry.WaterIntakeLitres > 0)
                    sb.Append($"Water: {entry.WaterIntakeLitres:0.0}L. ");
                if (!string.IsNullOrEmpty(entry.GeneralNotes))
                    sb.Append($"Notes: {entry.GeneralNotes} ");
                sb.AppendLine();
            }
            return sb.ToString().Trim();
        }
    }
}
