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
        public async Task<string> GenerateWorkoutAsync(Models.UserHealthContext context)
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
            Weekly History: {context.WeeklyHistoryBrief}
            Preferences/Conditions: {context.ConditionsBrief}
            InBody Analysis (secondary guidance): {context.InBodyBrief}
            
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
        public async Task<string> GenerateRecoveryDietJsonAsync(string upcomingWorkoutPlan, Models.UserHealthContext context)
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
            
            FOOD PREFERENCES:
            {context.FoodPreferences}

            DIET HISTORY:
            {context.DietHistoryBrief}

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
