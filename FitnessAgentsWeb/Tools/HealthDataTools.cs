using FitnessAgentsWeb.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Text.Json;

namespace FitnessAgentsWeb.Tools;

public static class HealthState
{
    // Variables for the Email HTML Dashboard
    public static string VitalsSleepTotal { get; set; } = "--";
    public static string VitalsSleepDeep { get; set; } = "--";
    public static string VitalsRhr { get; set; } = "--";
    public static string VitalsSteps { get; set; } = "0";
    public static string VitalsDistance { get; set; } = "0.0 km";
    public static string VitalsCalories { get; set; } = "0 kcal";

    public static string InBodyWeight { get; set; } = "--";
    public static string InBodyBf { get; set; } = "--";
    public static string InBodySmm { get; set; } = "--";
    public static string InBodyBmr { get; set; } = "--";
    public static string InBodyImbalances { get; set; } = "Balanced";
    public static string InBodyFatTarget { get; set; } = "0.0";

    // Pre-calculated Briefs for the AI Agent Prompt
    public static string ReadinessBrief { get; set; } = "Assume baseline readiness.";
    public static string InBodyBrief { get; set; } = "Assume standard baseline.";
    public static string ConditionsBrief { get; set; } = "No reported pain or injuries.";
    public static string WeeklyHistoryBrief { get; set; } = "No workouts recorded yet this week.";
}

public static class HealthDataTools
{
    [Description("Fetches today's smart ring data: Total Sleep, Deep Sleep, Resting HR, Steps, and Calories Burned.")]
    public static string GetDailyReadiness() => HealthState.ReadinessBrief;

    [Description("Fetches the user's latest InBody scan metrics: Body Fat, BMR, Fat Loss Targets, and Muscular Imbalances.")]
    public static string GetInBodyBaseline() => HealthState.InBodyBrief;

    [Description("Fetches the user's current physical conditions, pain points, or injuries.")]
    public static string GetUserConditions() => HealthState.ConditionsBrief;

    [Description("Fetches the intended gym workout schedule and target muscle groups for today.")]
    public static string GetWorkoutSchedule()
    {
        string today = DateTime.Now.DayOfWeek.ToString();
        var schedule = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Monday", "Fasting" }, { "Tuesday", "Chest and Triceps" },
            { "Wednesday", "Back and Biceps" }, { "Thursday", "Shoulders" },
            { "Friday", "Core and Abs" }, { "Saturday", "Legs" }, { "Sunday", "Active Recovery" }
        };
        if (schedule.TryGetValue(today, out string targetWorkout)) return $"Today is {today}. Scheduled Focus: {targetWorkout}.";
        return $"Today is {today}. Do a full-body routine.";
    }

    [Description("Fetches the workout history for the current week to avoid repeating exercises and ensure balanced programming.")]
    public static string GetWeeklyWorkoutHistory() => HealthState.WeeklyHistoryBrief;
}
