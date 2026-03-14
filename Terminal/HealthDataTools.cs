using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace Terminal;

public static class HealthDataTools
{
    // Make sure these paths point to where your automated exports actually land
    private const string HealthConnectFilePath = @"C:\Users\kiraj\Downloads\health_data_20260314_124302.json";
    private const string InBodyFilePath = @"C:\Users\kiraj\Downloads\latest_inbody.json";
    private const string ConditionsFilePath = @"C:\Users\kiraj\Downloads\user_condition.txt";

    [Description("Fetches today's Gabit smart ring data (Sleep, Resting HR) and calculates CNS fatigue.")]
    public static string GetDailyReadiness()
    {
        try
        {
            if (!File.Exists(HealthConnectFilePath))
                return "Error: Today's health data file is missing. Assume baseline readiness.";

            string json = File.ReadAllText(HealthConnectFilePath);
            var payload = JsonSerializer.Deserialize<HealthExportPayload>(json);

            if (payload == null) return "Error: Could not parse health data.";

            // --- 1. SLEEP CALCULATION ---
            string sleepString = "No recent sleep data";
            if (payload.Sleep.Any())
            {
                // Find the most recent sleep entry to anchor our "24-hour" window
                DateTime latestSleepTime = payload.Sleep.Max(s => s.SessionEndTime);

                var recentSleepSessions = payload.Sleep
                    .Where(s => s.SessionEndTime >= latestSleepTime.AddHours(-24))
                    .ToList();

                int totalSleepSeconds = recentSleepSessions.Sum(s => s.DurationSeconds);
                int sleepHours = totalSleepSeconds / 3600;
                int sleepMinutes = (totalSleepSeconds % 3600) / 60;

                sleepString = $"{sleepHours}h {sleepMinutes}m";
            }

            // --- 2. RESTING HEART RATE (RHR) & CNS STATUS ---
            string rhrString = "Unknown";
            string cnsStatus = "Unknown";

            // Sort newest to oldest
            var rhrData = payload.RestingHeartRate.OrderByDescending(r => r.Time).ToList();

            if (rhrData.Count > 0)
            {
                int todaysRhr = rhrData.First().Bpm;

                if (rhrData.Count > 1)
                {
                    // Calculate baseline from the previous available days (up to 7)
                    double baselineRhr = rhrData.Skip(1).Take(7).Average(r => r.Bpm);
                    double rhrDifference = todaysRhr - baselineRhr;

                    cnsStatus = "Optimal (Well Recovered)";
                    if (rhrDifference >= 4) cnsStatus = "Highly Fatigued (Elevated RHR)";
                    else if (rhrDifference >= 2) cnsStatus = "Moderately Fatigued";
                    else if (rhrDifference <= -2) cnsStatus = "Extremely Prime (Low RHR)";

                    rhrString = $"Today: {todaysRhr} bpm (7-Day Baseline: {Math.Round(baselineRhr, 1)} bpm)";
                }
                else
                {
                    // Fallback if we only have exactly 1 day of data
                    rhrString = $"Today: {todaysRhr} bpm";
                    cnsStatus = "Baseline Established (Need more data to calculate trend)";
                }
            }

            // We look at yesterday's load to figure out what muscles to rest today
            DateTime yesterday = DateTime.UtcNow.AddDays(-1);

            // Sum up steps
            int yesterdaySteps = payload.Steps
                .Where(s => s.EndTime >= yesterday)
                .Sum(s => s.Count);

            // Find any logged workouts
            var yesterdayWorkouts = payload.Exercise
                .Where(e => e.StartTime >= yesterday)
                .ToList();

            string workoutStrain = "None logged.";
            if (yesterdayWorkouts.Any())
            {
                int totalWorkoutMins = yesterdayWorkouts.Sum(e => e.DurationSeconds) / 60;

                // Map common Health Connect IDs (79 = Walking, 8 = Biking, 56 = Running, 82 = Weightlifting)
                var types = yesterdayWorkouts.Select(e =>
                    e.Type == "79" ? "Walking" :
                    e.Type == "8" ? "Biking" :
                    $"Type ID {e.Type}").Distinct();

                workoutStrain = $"{totalWorkoutMins} total minutes ({string.Join(", ", types)})";
            }

            // --- 4. THE ULTIMATE REPORT ---
            string finalReport =
                $"SYSTEMIC READINESS: Sleep: {sleepString}. RHR Trend: {rhrString}. CNS Status: {cnsStatus}. " +
                $"LOCALIZED STRAIN (Yesterday): Steps: {yesterdaySteps}. Workouts: {workoutStrain}.";

            return finalReport;
        }
        catch (Exception ex)
        {
            return $"Error crunching ring data: {ex.Message}";
        }
    }

    [Description("Fetches the user's latest InBody scan metrics, including body fat, BMR, and muscular imbalances.")]
    public static string GetInBodyBaseline()
    {
        try
        {
            if (!File.Exists(InBodyFilePath))
                return "Error: InBody scan data is missing. Assume standard baseline.";

            string json = File.ReadAllText(InBodyFilePath);
            var scan = JsonSerializer.Deserialize<InBodyExport>(json);

            if (scan == null) return "Error: Could not parse InBody data.";

            // 1. Format Core Metrics
            string coreStats = $"Weight: {scan.Core.WeightKg}kg. " +
                               $"Body Fat: {scan.Core.Pbf}%. " +
                               $"Skeletal Muscle Mass: {scan.Core.SmmKg}kg. " +
                               $"BMR: {scan.Metabolism.Bmr} kcal.";

            // 2. Identify Goals
            string targets = $"Goal: Lose {Math.Abs(scan.Targets.FatControl)}kg Fat, " +
                             $"Gain {scan.Targets.MuscleControl}kg Muscle.";

            // 3. Flag Muscular Imbalances (The most important part for programming)
            var weakPoints = new List<string>();
            if (scan.LeanBalance.LeftLeg == "Under" || scan.LeanBalance.RightLeg == "Under")
                weakPoints.Add("Legs (Lower Body)");
            if (scan.LeanBalance.LeftArm == "Under" || scan.LeanBalance.RightArm == "Under")
                weakPoints.Add("Arms (Upper Body)");
            if (scan.LeanBalance.Trunk == "Under")
                weakPoints.Add("Core/Trunk");

            string imbalances = weakPoints.Any()
                ? $"Critical Focus Areas (Underdeveloped): {string.Join(", ", weakPoints)}."
                : "Muscular balance is normal/even.";

            // 4. Assemble the brief for the LLM
            string finalBaseline = $"[INBODY SCAN DATA ({scan.ScanDate})]\n" +
                                   $"- {coreStats}\n" +
                                   $"- {targets}\n" +
                                   $"- {imbalances}";

            Console.WriteLine($"\n[System Data Extraction] InBody loaded: {scan.Core.Pbf}% BF, {scan.Targets.FatControl}kg Fat Target.");

            return finalBaseline;
        }
        catch (Exception ex)
        {
            return $"Error reading InBody data: {ex.Message}";
        }
    }

    [Description("Fetches the user's current physical conditions, pain points, or injuries.")]
    public static string GetUserConditions()
    {
        try
        {
            if (File.Exists(ConditionsFilePath))
            {
                string conditions = File.ReadAllText(ConditionsFilePath);
                return string.IsNullOrWhiteSpace(conditions)
                    ? "No reported pain or injuries."
                    : $"Reported Conditions: {conditions}";
            }
            return "No reported pain or injuries. Assume healthy.";
        }
        catch (Exception ex)
        {
            return $"Error reading conditions: {ex.Message}";
        }
    }

    [Description("Fetches the user's intended gym workout schedule and target muscle groups for today.")]
    public static string GetWorkoutSchedule()
    {
        // Get today's day of the week dynamically
        string today = DateTime.Now.DayOfWeek.ToString();

        // Hardcode your preferred split (or you could read this from a JSON config file)
        var schedule = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Monday", "Fasting" },
            { "Tuesday", "Chest and Triceps" },
            { "Wednesday", "Back and Biceps" },
            { "Thursday", "Shoulders" },
            { "Friday", "Core and Abs" },
            { "Saturday", "Legs" },
            { "Sunday", "Active Recovery" }
        };

        if (schedule.TryGetValue(today, out string targetWorkout))
        {
            return $"Today is {today}. Scheduled Focus: {targetWorkout}.";
        }

        return $"Today is {today}. No specific schedule found. Do a full-body routine.";
    }
}
