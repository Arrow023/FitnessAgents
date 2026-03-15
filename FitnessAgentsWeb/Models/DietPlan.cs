using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FitnessAgentsWeb.Models
{
    public class DietPlan
    {
        [JsonPropertyName("plan_date")]
        public DateTime PlanDate { get; set; }

        [JsonPropertyName("total_calories_target")]
        public int TotalCaloriesTarget { get; set; }

        [JsonPropertyName("meals")]
        public List<DietMeal> Meals { get; set; } = new();

        [JsonPropertyName("ai_summary")]
        public string AiSummary { get; set; } = string.Empty;
    }

    public class DietMeal
    {
        [JsonPropertyName("meal_type")]
        public string MealType { get; set; } = string.Empty; // Morning, Lunch, Evening, Dinner

        [JsonPropertyName("food_name")]
        public string FoodName { get; set; } = string.Empty;

        [JsonPropertyName("quantity_description")]
        public string QuantityDescription { get; set; } = string.Empty;

        [JsonPropertyName("calories")]
        public int Calories { get; set; }
    }
}
