namespace FitnessAgentsWeb.Models
{
    public class UserProfile
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string NotificationTime { get; set; } = "08:00"; // HH:mm 24-hour format
        public string Preferences { get; set; } = "No reported pain or injuries."; // Used for AI ConditionsBrief
        public string FoodPreferences { get; set; } = "No specific food preferences."; 
        public List<string> ExcludedFoods { get; set; } = new();
        public string CuisineStyle { get; set; } = string.Empty;
        public List<string> CookingOils { get; set; } = new();
        public List<string> StapleGrains { get; set; } = new();
        public bool IsActive { get; set; } = true;
        public bool IsAdmin { get; set; } = false;
        public int Age { get; set; } = 30; // Used for VO2max estimation (220 - Age = HRmax)

        public string? WebhookHeaderKey { get; set; }
        public string? WebhookHeaderValue { get; set; }

        /// <summary>
        /// Tracks which onboarding steps have been completed via the chat agent.
        /// Steps: "name", "age", "conditions", "food", "cuisine", "schedule"
        /// </summary>
        private List<string>? _onboardingCompleted = new();
        public List<string> OnboardingCompleted
        {
            get => _onboardingCompleted ??= new();
            set => _onboardingCompleted = value ?? new();
        }

        /// <summary>
        /// Whether the user has completed the minimum onboarding (name + age + conditions).
        /// </summary>
        public bool IsOnboardingComplete { get; set; }

        public System.Collections.Generic.Dictionary<string, string> WorkoutSchedule { get; set; } = new ()
        {
            { "Monday", "Fasting" }, { "Tuesday", "Chest and Triceps" },
            { "Wednesday", "Back and Biceps" }, { "Thursday", "Shoulders" },
            { "Friday", "Core and Abs" }, { "Saturday", "Legs" }, { "Sunday", "Active Recovery" }
        };
    }
}
