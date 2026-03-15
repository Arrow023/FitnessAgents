namespace FitnessAgentsWeb.Models
{
    public class UserHealthContext
    {
        public string UserId { get; set; } = string.Empty;
        public string FirstName { get; set; } = "the client";
        public string Email { get; set; } = string.Empty;

        // Variables for the Email HTML Dashboard
        public string VitalsSleepTotal { get; set; } = "--";
        public string VitalsSleepDeep { get; set; } = "--";
        public string VitalsRhr { get; set; } = "--";
        public string VitalsSteps { get; set; } = "0";
        public string VitalsDistance { get; set; } = "0.0 km";
        public string VitalsCalories { get; set; } = "0 kcal";
        public string VitalsHrv { get; set; } = "--";
        public string VitalsTotalCalories { get; set; } = "0";

        public string InBodyWeight { get; set; } = "--";
        public string InBodyBf { get; set; } = "--";
        public string InBodySmm { get; set; } = "--";
        public string InBodyBmr { get; set; } = "--";
        public string InBodyImbalances { get; set; } = "Balanced";
        public string InBodyFatTarget { get; set; } = "0.0";
        public string InBodyVisceral { get; set; } = "0";
        public string InBodyBmi { get; set; } = "0.0";

        // Pre-calculated Briefs for the AI Agent Prompt
        public string ReadinessBrief { get; set; } = "Assume baseline readiness.";
        public string InBodyBrief { get; set; } = "Assume standard baseline.";
        public string ConditionsBrief { get; set; } = "No reported pain or injuries.";
        public string WeeklyHistoryBrief { get; set; } = "No workouts recorded yet this week.";
        public string FoodPreferences { get; set; } = "No specific food preferences.";
        public string DietHistoryBrief { get; set; } = "No previous diet history for this week.";
        public System.Collections.Generic.Dictionary<string, string> WorkoutSchedule { get; set; } = new();
    }
}
