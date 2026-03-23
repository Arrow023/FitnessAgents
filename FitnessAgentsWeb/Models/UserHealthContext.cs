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

        // Phase 2: Expanded Vitals
        public string VitalsBloodPressure { get; set; } = "--/--";
        public string VitalsSpO2 { get; set; } = "--";
        public string VitalsVo2Max { get; set; } = "--";
        public string VitalsRespiratoryRate { get; set; } = "--";
        public string VitalsHydration { get; set; } = "0.0";
        public string VitalsNutritionCalories { get; set; } = "0";
        public string VitalsProtein { get; set; } = "0";
        public string VitalsCarbs { get; set; } = "0";
        public string VitalsFat { get; set; } = "0";

        // Phase 3: Computed Insights
        public string SleepEfficiency { get; set; } = "--";
        public string CalorieBalance { get; set; } = "--";
        public int RecoveryScore { get; set; } = 0;
        public int SleepScore { get; set; } = 0;
        public int ActiveScore { get; set; } = 0;
        public string ActiveMinutesWeekly { get; set; } = "0";
        public string ExerciseLog { get; set; } = "[]";

        // 7-day trend data (JSON arrays for sparklines)
        public string RhrTrend { get; set; } = "[]";
        public string HrvTrend { get; set; } = "[]";
        public string StepsTrend { get; set; } = "[]";
        public string SleepTrend { get; set; } = "[]";

        // 15-day averages
        public string AvgRhr15Day { get; set; } = "--";
        public string AvgHrv15Day { get; set; } = "--";
        public string AvgSteps15Day { get; set; } = "--";
        public string AvgSleep15Day { get; set; } = "--";

        public string InBodyWeight { get; set; } = "--";
        public string InBodyBf { get; set; } = "--";
        public string InBodySmm { get; set; } = "--";
        public string InBodyBmr { get; set; } = "--";
        public string InBodyImbalances { get; set; } = "Balanced";
        public string InBodyFatTarget { get; set; } = "0.0";
        public string InBodyVisceral { get; set; } = "0";
        public string InBodyBmi { get; set; } = "0.0";
        public string InBodyFatControl { get; set; } = "0.0";
        public string InBodyMuscleControl { get; set; } = "0.0";
        public string InBodyScanDate { get; set; } = "--";

        // Phase 4: UX
        public string DataTimestamp { get; set; } = "--";

        // Pre-calculated Briefs for the AI Agent Prompt
        public string ReadinessBrief { get; set; } = "Assume baseline readiness.";
        public string InBodyBrief { get; set; } = "Assume standard baseline.";
        public string ConditionsBrief { get; set; } = "No reported pain or injuries.";
        public string WeeklyHistoryBrief { get; set; } = "No workouts recorded yet this week.";
        public string FoodPreferences { get; set; } = "No specific food preferences.";
        public List<string> ExcludedFoods { get; set; } = new();
        public string CuisineStyle { get; set; } = string.Empty;
        public List<string> CookingOils { get; set; } = new();
        public List<string> StapleGrains { get; set; } = new();
        public string DiaryBrief { get; set; } = string.Empty;
        public string YesterdayExerciseBrief { get; set; } = "No exercise sessions logged yesterday.";
        public string DietHistoryBrief { get; set; } = "No previous diet history for this week.";
        public System.Collections.Generic.Dictionary<string, string> WorkoutSchedule { get; set; } = new();
    }
}
