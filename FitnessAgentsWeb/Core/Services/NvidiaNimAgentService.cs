using FitnessAgentsWeb.Core.Configuration;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Tools;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OpenAI;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using System.ClientModel;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Core.Services
{
    public class NvidiaNimAgentService : BaseAiAgentService, IAiAgentService
    {
        public NvidiaNimAgentService(IAppConfigurationProvider configProvider, ILogger<NvidiaNimAgentService> logger)
            : base(configProvider, logger)
        {
        }
        public async Task<string> GenerateWorkoutAsync(Models.UserHealthContext context, string similarPlansContext = "", string recentFeedbackContext = "", string digestContext = "")
        {
            IChatClient chatClient = GetChatClient();
            var now = GetAppNow();
            string todayDate = now.ToString("dddd, MMM dd, yyyy");
            string todayDay = now.ToString("dddd");

            // Build today's scheduled focus from the user's weekly schedule
            string todayScheduledFocus = context.WorkoutSchedule.TryGetValue(todayDay, out var focus) ? focus : "General Fitness";
            string scheduleOverview = context.WorkoutSchedule.Any()
                ? string.Join(" | ", context.WorkoutSchedule.Select(kvp => $"{kvp.Key}: {kvp.Value}"))
                : "No weekly schedule set.";

            string prompt = $@"STRICT RESTRCTION: DO NOT include any reasoning, thought process, or preamble. Return ONLY the JSON object.
            
            You are an elite Strength & Conditioning Coach. 
            Create a professional training session for {context.FirstName}.
            TODAY'S DATE: {todayDate}

            WEEKLY SCHEDULE (user-defined, MUST be followed):
            {scheduleOverview}

            TODAY'S SCHEDULED FOCUS: {todayScheduledFocus}
            IMPORTANT: The workout MUST target ""{todayScheduledFocus}"" as defined in the user's weekly schedule.
            If the schedule says Fasting, Active Recovery, or Rest Day, generate a light mobility/stretching session accordingly.
            InBody focus areas below are secondary guidance — incorporate them only where they align with today's scheduled focus.
            
            USER PROFILE:
            Weight: {context.InBodyWeight}kg
            Body Fat: {context.InBodyBf}%
            Recent Activity: {context.ReadinessBrief}
            Yesterday's Exercise: {context.YesterdayExerciseBrief}
            Weekly History: {context.WeeklyHistoryBrief}
            Preferences/Conditions: {context.ConditionsBrief}
            InBody Analysis (secondary guidance): {context.InBodyBrief}
            {(string.IsNullOrEmpty(context.DiaryBrief) ? "" : $"\n═══ DIARY PATTERNS (recent user logs) ═══\n{context.DiaryBrief}\n")}{(string.IsNullOrEmpty(digestContext) ? "" : $"\n═══ LONG-TERM BEHAVIORAL PATTERNS (weekly digests) ═══\n{digestContext}INSTRUCTIONS: These are aggregated weekly patterns from the user's diary history. Use them to identify recurring trends — e.g. exercises they frequently skip, persistent pain areas to avoid, consistent energy/mood patterns.\n")}{(string.IsNullOrEmpty(similarPlansContext) ? "" : $"\nPAST SUCCESSFUL PLANS (similar context, user-rated):\n{similarPlansContext}\n")}{(string.IsNullOrEmpty(recentFeedbackContext) ? "" : $"\nRECENT USER FEEDBACK:\n{recentFeedbackContext}\nINSTRUCTIONS: Learn from the user's preferences above. Incorporate patterns from highly-rated plans. Avoid patterns from poorly-rated plans or skipped exercises.\n")}
            
            JSON Schema:
            {{
                ""plan_date"": ""string (ISO8601)"",
                ""session_title"": ""string"",
                ""personalized_introduction"": ""string (Highly personalized, encouraging opening message acknowledging user's latest health data/efforts and negatives if any)"",
                ""warmup"": [
                    {{ ""exercise"": ""string"", ""instruction"": ""string"" }}
                ],
                ""main_workout"": [
                    {{ ""exercise"": ""string"", ""sets"": ""number"", ""reps"": ""string"", ""rest"": ""string"", ""notes"": ""string"" }}
                ],
                ""cooldown"": [
                    {{ ""exercise"": ""string"", ""duration"": ""string"" }}
                ],
                ""coach_notes"": ""string""
            }}";

            AIAgent coachAgent = chatClient.AsAIAgent(
                name: "Strength_Coach",
                instructions: "You are the world's best personal trainer with a warm, expert, and encouraging personality. Your output MUST be a valid JSON object. The 'personalized_introduction' should be your direct message to the user, capturing your appreciation for their efforts and analysis of their readiness. Do NOT be robotic; be the elite coach they know."
            );

            AgentSession session = await coachAgent.CreateSessionAsync();
            _logger.LogInformation("[Workout AI] Agent is drafting structured plan...");

            try
            {
                string aiResponse = "";
                await foreach (var update in coachAgent.RunStreamingAsync(prompt, session))
                {
                    if (update.Text != null) aiResponse += update.Text;
                }

                _logger.LogInformation($"[Workout AI Raw Response] {aiResponse}");
                return ExtractJson(aiResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Workout AI] Error generating plan");
                return "{}";
            }
        }
        public async Task<string> GenerateRecoveryDietJsonAsync(string upcomingWorkoutPlan, Models.UserHealthContext context, string similarPlansContext = "", string recentFeedbackContext = "", string digestContext = "")
        {
            IChatClient chatClient = GetChatClient();
            string todayDate = GetAppNow().ToString("dddd, MMM dd, yyyy");

            string prompt = $@"STRICT RESTRCTION: DO NOT include any reasoning, thought process, or preamble. Return ONLY the JSON object.
            
            You are a leading Clinical Sports Nutritionist.
            TODAY'S DATE: {todayDate}
            Your client, {context.FirstName}, has just received the following training protocol for today:
            
            WORKOUT PLAN:
            {upcomingWorkoutPlan}

            USER BASELINE:
            Weight: {context.InBodyWeight} kg
            Body Fat: {context.InBodyBf}%
            BMR: {context.InBodyBmr} kcal
            Active Calories Burned Today: {context.VitalsCalories}
            Total Calories Burned Today: {context.VitalsTotalCalories}
            
            ═══ HARD CONSTRAINTS (NEVER VIOLATE — violation = plan rejection) ═══
            {(context.ExcludedFoods.Count > 0 ? $"EXCLUDED FOODS (NEVER include any of these): {string.Join(", ", context.ExcludedFoods)}" : "No food exclusions.")}
            {(context.CookingOils.Count > 0 ? $"ALLOWED COOKING OILS ONLY: {string.Join(", ", context.CookingOils)}. Do NOT use any other oils." : "")}
            {(context.StapleGrains.Count > 0 ? $"STAPLE GRAINS (prefer these): {string.Join(", ", context.StapleGrains)}" : "")}

            ═══ CULTURAL CONTEXT ═══
            {(string.IsNullOrEmpty(context.CuisineStyle) ? "No specific cuisine style." : $"Cuisine: {context.CuisineStyle} — ALL meals MUST use culturally authentic ingredients, recipes, and cooking methods from this cuisine. Do NOT default to generic Western health food.")}

            ═══ SOFT PREFERENCES (respect when possible) ═══
            {context.FoodPreferences}
            {(string.IsNullOrEmpty(context.DiaryBrief) ? "" : $"\n═══ DIARY PATTERNS (what the user actually eats/does) ═══\n{context.DiaryBrief}\n")}{(string.IsNullOrEmpty(digestContext) ? "" : $"\n═══ LONG-TERM BEHAVIORAL PATTERNS (weekly digests) ═══\n{digestContext}INSTRUCTIONS: These weekly patterns reveal long-term food habits. Repeat consistent meals the user already eats regularly. Avoid foods from weeks with low ratings. Adapt portions to match actual consumption patterns.\n")}
            DIET HISTORY:
            {context.DietHistoryBrief}
            {(string.IsNullOrEmpty(similarPlansContext) ? "" : $"\nPAST SUCCESSFUL DIET PLANS (similar context, user-rated):\n{similarPlansContext}\n")}{(string.IsNullOrEmpty(recentFeedbackContext) ? "" : $"\nRECENT USER FEEDBACK:\n{recentFeedbackContext}\nINSTRUCTIONS: Learn from the user's meal preferences. Repeat well-rated meals. Avoid items the user skipped.\n")}
            Provide a 4-meal macro-optimized diet plan for today.
            
            JSON Schema:
            {{
                ""plan_date"": ""string (ISO8601 format)"",
                ""total_calories_target"": ""number (total daily target)"",
                ""meals"": [
                    {{ ""meal_type"": ""string (e.g., Morning, Lunch, Evening, Dinner)"", ""food_name"": ""string"", ""quantity_description"": ""string (e.g., 200g, 1 cup)"", ""calories"": ""number"" }}
                ],
                ""ai_summary"": ""string (Brief expert rationale)""
            }}";

            AIAgent dieticianAgent = chatClient.AsAIAgent(
                name: "Dietician_Planner",
                instructions: "You are an elite sports nutritionist. Your output MUST be a valid JSON object matching the requested schema. Use the schema values as type indicators. DO NOT copy example text. Provide real nutritional data for this specific client."
            );

            AgentSession session = await dieticianAgent.CreateSessionAsync();
            _logger.LogInformation("[Diet AI] Agent is planning recovery diet...");

            try
            {
                string aiResponse = "";
                await foreach (var update in dieticianAgent.RunStreamingAsync(prompt, session))
                {
                    if (update.Text != null)
                    {
                        aiResponse += update.Text;
                    }
                }
                
                _logger.LogInformation($"[Diet AI Raw Response] {aiResponse}");

                return ExtractJson(aiResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Diet AI] Error generating diet: {ex.Message}");
                return "{}";
            }
        }
    }
}
