using FitnessAgentsWeb.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace FitnessAgentsWeb.Tools
{
    public class HealthDataTools
    {
        private readonly UserHealthContext _context;

        public HealthDataTools(UserHealthContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        [Description("Fetches today's smart ring data: Total Sleep, Deep Sleep, Resting HR, Steps, and Calories Burned.")]
        public string GetDailyReadiness() => _context.ReadinessBrief;

        [Description("Fetches the user's latest InBody scan metrics: Body Fat, BMR, Fat Loss Targets, and Muscular Imbalances.")]
        public string GetInBodyBaseline() => _context.InBodyBrief;

        [Description("Fetches the user's current physical conditions, pain points, or injuries.")]
        public string GetUserConditions() => _context.ConditionsBrief;

        [Description("Fetches the intended gym workout schedule and target muscle groups for today.")]
        public string GetWorkoutSchedule()
        {
            string today = DateTime.Now.DayOfWeek.ToString();
            
            if (_context.WorkoutSchedule != null && _context.WorkoutSchedule.TryGetValue(today, out string targetWorkout))
            {
                return $"Today is {today}. Scheduled Focus: {targetWorkout}.";
            }

            return $"Today is {today}. Do a full-body routine.";
        }

        [Description("Fetches the workout history for the current week to avoid repeating exercises and ensure balanced programming.")]
        public string GetWeeklyWorkoutHistory() => _context.WeeklyHistoryBrief;

        [Description("Fetches yesterday's exercise sessions from health monitoring data (e.g. walking, cycling, gym) — only the previous day, not the full timeline.")]
        public string GetYesterdayExercise() => _context.YesterdayExerciseBrief;
    }
}
