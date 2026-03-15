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

            DateTime cutoff = DateTime.UtcNow.AddDays(-7);

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
                    .Where(e => e.StartTime >= cutoff).ToList()
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

                DateTime activityStart = targetDateIst;
                DateTime activityEnd = targetDateIst.AddDays(1);
                DateTime sleepStart = targetDateIst.AddHours(-12);
                DateTime sleepEnd = targetDateIst.AddHours(12);

                bool IsTargetActivity(DateTime utcTime) => TimeZoneInfo.ConvertTimeFromUtc(utcTime, _istZone) >= activityStart && TimeZoneInfo.ConvertTimeFromUtc(utcTime, _istZone) < activityEnd;
                bool IsTargetSleep(DateTime utcTime) => TimeZoneInfo.ConvertTimeFromUtc(utcTime, _istZone) >= sleepStart && TimeZoneInfo.ConvertTimeFromUtc(utcTime, _istZone) < sleepEnd;

                var targetSleepSessions = hc.Sleep.Where(s => IsTargetSleep(s.SessionEndTime)).ToList();
                int totalSleepSecs = targetSleepSessions.SelectMany(s => s.Stages).Where(st => st.Stage != "1" && st.Stage != "2").Sum(st => st.DurationSeconds);
                context.VitalsSleepTotal = $"{totalSleepSecs / 3600}h {(totalSleepSecs % 3600) / 60}m";

                int deepSleepSecs = targetSleepSessions.SelectMany(s => s.Stages).Where(st => st.Stage == "4").Sum(st => st.DurationSeconds);
                context.VitalsSleepDeep = $"{deepSleepSecs / 3600}h {(deepSleepSecs % 3600) / 60}m";

                var targetHrv = hc.HRV.Where(h => IsTargetActivity(h.Time)).OrderByDescending(h => h.Time).FirstOrDefault();
                context.VitalsHrv = targetHrv != null ? Math.Round(targetHrv.Rmssd, 0).ToString() : "--";

                var latestSteps = hc.Steps.Where(s => IsTargetActivity(s.EndTime)).OrderByDescending(s => s.EndTime).FirstOrDefault();
                context.VitalsSteps = latestSteps != null ? latestSteps.Count.ToString("N0") : "0";

                var latestDist = hc.Distance.Where(d => IsTargetActivity(d.EndTime)).OrderByDescending(d => d.EndTime).FirstOrDefault();
                context.VitalsDistance = latestDist != null ? (latestDist.Meters / 1000.0).ToString("0.00") + " km" : "0.00 km";

                var latestActiveCals = hc.ActiveCalories.Where(c => IsTargetActivity(c.EndTime)).OrderByDescending(c => c.EndTime).FirstOrDefault();
                var latestTotalCals = hc.TotalCalories.Where(c => IsTargetActivity(c.EndTime)).OrderByDescending(c => c.EndTime).FirstOrDefault();

                context.VitalsCalories = latestActiveCals != null ? Math.Round(latestActiveCals.Calories, 0).ToString("N0") + " kcal" : "0 kcal";
                context.VitalsTotalCalories = latestTotalCals != null ? Math.Round(latestTotalCals.Calories, 0).ToString("N0") + " kcal" : "0 kcal";

                var targetRhr = hc.RestingHeartRate.Where(r => IsTargetActivity(r.Time)).OrderByDescending(r => r.Time).FirstOrDefault();
                context.VitalsRhr = targetRhr != null ? targetRhr.Bpm.ToString() : "--";

                context.ReadinessBrief = $"[TARGET DAY: {targetDateIst:MMM dd}] Sleep: {context.VitalsSleepTotal} (Deep: {context.VitalsSleepDeep}). RHR: {context.VitalsRhr} bpm. HRV: {context.VitalsHrv}. Steps: {context.VitalsSteps}. Active Burn: {context.VitalsCalories} (Total: {Math.Round(latestTotalCals?.Calories ?? 0, 0)} kcal).";
            }

            // Load Weekly History Brief
            var weeklyHistory = await _storageRepository.GetWeeklyHistoryAsync(userId);
            if (weeklyHistory != null && weeklyHistory.PastWorkouts.Any())
            {
                var summaries = weeklyHistory.PastWorkouts.Select(kvp => $"{kvp.Key}: {kvp.Value}");
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
                if (profile.WorkoutSchedule != null && profile.WorkoutSchedule.Any())
                {
                    context.WorkoutSchedule = profile.WorkoutSchedule;
                }
            }
            else
            {
                _logger.LogWarning($"[HealthProcessor] Profile NOT FOUND for {userId}");
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
                    context.InBodyBmi = scan.Core.Bmi.ToString();

                    var weak = new List<string>();
                    if (scan.LeanBalance.LeftLeg == "Under" || scan.LeanBalance.RightLeg == "Under") weak.Add("Legs");
                    if (scan.LeanBalance.LeftArm == "Under" || scan.LeanBalance.RightArm == "Under") weak.Add("Arms");
                    if (scan.LeanBalance.Trunk == "Under") weak.Add("Core");
                    context.InBodyImbalances = weak.Any() ? string.Join(", ", weak) : "Balanced";

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
    }
}
