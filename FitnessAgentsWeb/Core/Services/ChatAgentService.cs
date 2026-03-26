using FitnessAgentsWeb.Core.Configuration;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models;
using FitnessAgentsWeb.Tools;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace FitnessAgentsWeb.Core.Services
{
    /// <summary>
    /// Chat agent that uses tool-calling to read and modify user data.
    /// Streams events (thinking, tool calls, text) to the client via SSE.
    /// </summary>
    public class ChatAgentService : BaseAiAgentService, IChatAgentService
    {
        private readonly IStorageRepository _storage;
        private readonly IHealthDataProcessor _healthProcessor;
        private readonly IAppNotificationStore _notifications;
        private readonly IPlanGenerationTracker _jobTracker;

        public ChatAgentService(
            IAppConfigurationProvider configProvider,
            IStorageRepository storage,
            IHealthDataProcessor healthProcessor,
            IAppNotificationStore notifications,
            IPlanGenerationTracker jobTracker,
            ILogger<ChatAgentService> logger)
            : base(configProvider, logger)
        {
            _storage = storage;
            _healthProcessor = healthProcessor;
            _notifications = notifications;
            _jobTracker = jobTracker;
        }

        public async IAsyncEnumerable<ChatStreamEvent> ProcessMessageAsync(
            string userId,
            string userMessage,
            List<ChatHistoryMessage> conversationHistory,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var tools = new ChatAgentTools(_storage, _healthProcessor, _notifications, _jobTracker, userId, _logger);
            IChatClient chatClient = GetChatClient();

            // Check onboarding status to inject per-turn guidance
            var profile = await _storage.GetUserProfileAsync(userId);
            bool isOnboarding = profile is not null && !profile.IsOnboardingComplete;
            string? onboardingInjection = isOnboarding ? BuildOnboardingInjection(profile!, conversationHistory) : null;

            var chatOptions = new ChatOptions
            {
                Tools = BuildToolDefinitions(tools),
                Temperature = isOnboarding ? 0.3f : 0.7f // Lower temperature for deterministic tool-calling during onboarding
            };

            var messages = BuildMessageHistory(conversationHistory, userMessage, userId, onboardingInjection);

            // Emit initial thinking event
            yield return new ChatStreamEvent { Type = ChatStreamEventType.Thinking, Text = "Understanding your request..." };

            const int maxToolRounds = 8;
            int round = 0;

            while (round < maxToolRounds)
            {
                round++;
                cancellationToken.ThrowIfCancellationRequested();

                ChatResponse response;
                ChatStreamEvent? errorEvent = null;
                try
                {
                    response = await chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ChatAgent] Error calling AI for user {UserId}", userId);
                    errorEvent = new ChatStreamEvent { Type = ChatStreamEventType.Error, Text = "Sorry, I encountered an error processing your request." };
                    response = null!;
                }

                if (errorEvent is not null)
                {
                    yield return errorEvent;
                    yield break;
                }

                // Add the full assistant response to the conversation
                foreach (var msg in response.Messages)
                    messages.Add(msg);

                // Check if the model wants to call tools
                var toolCalls = response.Messages
                    .SelectMany(m => m.Contents.OfType<FunctionCallContent>())
                    .ToList();

                if (toolCalls.Count == 0)
                {
                    // No tool calls — extract the text response and stream it
                    string textResponse = string.Join("", response.Messages
                        .SelectMany(m => m.Contents.OfType<TextContent>())
                        .Select(t => t.Text));

                    if (!string.IsNullOrEmpty(textResponse))
                    {
                        yield return new ChatStreamEvent { Type = ChatStreamEventType.Message, Text = textResponse };
                    }
                    break;
                }

                // Process each tool call
                foreach (var toolCall in toolCalls)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string toolName = toolCall.Name;
                    var toolArgs = toolCall.Arguments;

                    yield return new ChatStreamEvent
                    {
                        Type = ChatStreamEventType.ToolCall,
                        ToolName = FormatToolDisplayName(toolName),
                        ToolArgs = toolArgs is not null
                            ? JsonSerializer.Serialize(toolArgs, new JsonSerializerOptions { WriteIndented = false })
                            : null
                    };

                    string result;
                    // Convert IDictionary to IReadOnlyDictionary for tool execution
                    IReadOnlyDictionary<string, object?>? readOnlyArgs = toolArgs is not null
                        ? new Dictionary<string, object?>(toolArgs)
                        : null;
                    try
                    {
                        result = await ExecuteToolAsync(tools, toolName, readOnlyArgs);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[ChatAgent] Tool execution failed: {ToolName}", toolName);
                        result = $"Error executing {toolName}: {ex.Message}";
                    }

                    yield return new ChatStreamEvent
                    {
                        Type = ChatStreamEventType.ToolResult,
                        ToolName = FormatToolDisplayName(toolName),
                        ToolResult = result
                    };

                    // Provide the tool result back to the AI
                    messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Tool, [new FunctionResultContent(toolCall.CallId, result)]));
                }
            }

            yield return new ChatStreamEvent { Type = ChatStreamEventType.Done };
        }

        private static List<AITool> BuildToolDefinitions(ChatAgentTools tools)
        {
            return
            [
                AIFunctionFactory.Create(tools.GetUserProfile),
                AIFunctionFactory.Create(tools.GetTodayDiary),
                AIFunctionFactory.Create(tools.GetHealthInsights),
                AIFunctionFactory.Create(tools.GetTodayPlans),
                AIFunctionFactory.Create(tools.GetRecentDiaryHistory),
                AIFunctionFactory.Create(tools.GetSleepDetails),
                AIFunctionFactory.Create(tools.GetExerciseHistory),
                AIFunctionFactory.Create(tools.GetWeeklyWorkoutPlanHistory),
                AIFunctionFactory.Create(tools.GetWeeklyDietPlanHistory),
                AIFunctionFactory.Create(tools.GetRecentFeedback),
                AIFunctionFactory.Create(tools.GetWeeklyDigest),
                AIFunctionFactory.Create(tools.GetInBodyAnalysis),
                AIFunctionFactory.Create(tools.GetNotifications),
                AIFunctionFactory.Create(tools.GetPlanGenerationStatus),
                AIFunctionFactory.Create(tools.UpdateFoodPreferences),
                AIFunctionFactory.Create(tools.UpdateConditions),
                AIFunctionFactory.Create(tools.UpdateWorkoutSchedule),
                AIFunctionFactory.Create(tools.UpsertDiaryEntry),
                AIFunctionFactory.Create(tools.SubmitPlanFeedback),
                AIFunctionFactory.Create(tools.UpdatePersonalInfo),
                AIFunctionFactory.Create(tools.GetOnboardingStatus),
                AIFunctionFactory.Create(tools.GetFrequentMeals),
            ];
        }

        private List<Microsoft.Extensions.AI.ChatMessage> BuildMessageHistory(List<ChatHistoryMessage> history, string userMessage, string userId, string? onboardingInjection)
        {
            var now = GetAppNow();
            string systemPrompt = $@"You are FitnessAgent, a friendly and knowledgeable personal health assistant.
Today's date: {now:dddd, MMMM dd, yyyy} | Time: {now:HH:mm}
User ID: {userId}

CORE BEHAVIOR:
- Be warm, encouraging, and concise. You are their personal health coach in a chat interface.
- When the user mentions something that affects their profile (allergies, food preferences, injuries, schedule changes), ALWAYS read the current state first using the appropriate GET tool, then apply changes using the UPDATE tool.
- NEVER blindly overwrite data. Always read first, then merge.
- When logging diary entries, ALWAYS call GetTodayDiary first to see what's already there, then add/merge new data.
- For health insights, use GetHealthInsights to provide data-driven responses.
- Format responses using markdown for readability (bold, lists, etc.).

ONBOARDING:
- During onboarding, you will receive a system instruction telling you exactly which tool to call. Follow it precisely.
- Be conversational and encouraging. Don't make it feel like a form.
- Ask only ONE question at a time.
- If the user says ""skip"" or ""I'll do this later"", respect that and move on.

TOOL USAGE RULES:
1. Before any UPDATE operation, call the corresponding GET tool first.
2. CRITICAL: When the user provides information (name, age, food preferences, conditions, schedule), you MUST call the appropriate save tool. Acknowledging without saving is NOT acceptable — the data will be lost.
3. For profile changes: GetUserProfile → then UpdatePersonalInfo / UpdateFoodPreferences / UpdateConditions / UpdateWorkoutSchedule
4. For diary entries: GetTodayDiary or GetRecentDiaryHistory → then UpsertDiaryEntry with the target date (merges, never replaces). If the user mentions a specific date (e.g. ""yesterday"", ""last Monday""), resolve it to yyyy-MM-dd and pass it as the date parameter.
5. For health questions: GetHealthInsights and/or GetTodayPlans
6. For sleep details: GetSleepDetails for stage breakdowns, efficiency, bedtime/wake time, vitals
7. For exercise questions: GetExerciseHistory for sessions tracked by their device
8. For weekly plans: GetWeeklyWorkoutPlanHistory / GetWeeklyDietPlanHistory for Mon-Sun schedule
9. For pattern analysis: GetRecentDiaryHistory and/or GetWeeklyDigest for behavioral trends
10. For body composition: GetInBodyAnalysis for segmental lean balance, targets, metabolic health
11. For past feedback: GetRecentFeedback to see how the user rated previous plans
12. For notifications: GetNotifications to check recent alerts
13. For plan status: GetPlanGenerationStatus to check if plans are being generated
14. When user provides plan feedback: SubmitPlanFeedback
15. For onboarding status: GetOnboardingStatus
16. For personal info (name/age): UpdatePersonalInfo
17. For meal suggestions: GetFrequentMeals to see frequently eaten foods

RESPONSE STYLE:
- Keep responses focused and actionable
- Use data from tools to personalize advice
- Acknowledge changes you've made clearly
- If unsure what the user wants modified, ask for clarification
- Use emoji sparingly for warmth (💪 🥗 😴 etc.)";

            var messages = new List<Microsoft.Extensions.AI.ChatMessage>
            {
                new(ChatRole.System, systemPrompt)
            };

            // Add conversation history (last 20 messages max to stay within context limits)
            int startIdx = Math.Max(0, history.Count - 20);
            for (int i = startIdx; i < history.Count; i++)
            {
                var msg = history[i];
                var role = msg.Role switch
                {
                    "user" => ChatRole.User,
                    "assistant" => ChatRole.Assistant,
                    _ => ChatRole.User
                };
                messages.Add(new Microsoft.Extensions.AI.ChatMessage(role, msg.Content));
            }

            // Inject per-turn onboarding context by prepending to the user's message
            string finalUserMessage = !string.IsNullOrEmpty(onboardingInjection)
                ? $"[SYSTEM CONTEXT: {onboardingInjection}]\n\nUser message: {userMessage}"
                : userMessage;

            messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, finalUserMessage));
            return messages;
        }

        /// <summary>
        /// Builds a targeted system injection for the current onboarding turn.
        /// Reads the profile's OnboardingCompleted list and tells the LLM exactly what to do next.
        /// </summary>
        private static string BuildOnboardingInjection(UserProfile profile, List<ChatHistoryMessage> history)
        {
            var completed = profile.OnboardingCompleted;
            bool isFirstMessage = history.Count == 0;

            // Determine the next incomplete step
            if (isFirstMessage)
            {
                return @"[ONBOARDING ACTIVE] The user just started onboarding. Welcome them warmly and ask: ""What's your name?"" Do NOT ask multiple questions. Wait for their answer.";
            }

            if (!completed.Contains("name", StringComparer.OrdinalIgnoreCase))
            {
                return @"[ONBOARDING ACTIVE] The user is answering the NAME question.
ACTION REQUIRED: Extract their first name (and last name if given) from the message.
You MUST call UpdatePersonalInfo with the firstName (and lastName if provided).
After saving, ask: ""How old are you?""
Do NOT skip the tool call. The data is lost if you don't call the tool.";
            }

            if (!completed.Contains("age", StringComparer.OrdinalIgnoreCase))
            {
                return @"[ONBOARDING ACTIVE] The user is answering the AGE question.
ACTION REQUIRED: Extract their age from the message.
You MUST call UpdatePersonalInfo with the age parameter.
After saving, ask: ""Do you have any injuries, health conditions, or areas of pain I should know about? (e.g. knee injury, back pain, diabetes)""
Do NOT skip the tool call.";
            }

            if (!completed.Contains("conditions", StringComparer.OrdinalIgnoreCase))
            {
                return @"[ONBOARDING ACTIVE] The user is answering the CONDITIONS/INJURIES question.
ACTION REQUIRED: Extract their conditions/injuries from the message.
You MUST call UpdateConditions with the conditions text. If they say none/nothing, call UpdateConditions with ""No reported pain or injuries.""
After saving, congratulate them — core onboarding is done! Then ask: ""Any foods you avoid or are allergic to?""
Do NOT skip the tool call.";
            }

            if (!completed.Contains("food", StringComparer.OrdinalIgnoreCase))
            {
                return @"[ONBOARDING ACTIVE] The user is answering the FOOD RESTRICTIONS question.
ACTION REQUIRED: Extract foods they avoid from the message.
You MUST call UpdateFoodPreferences with excludedFoodsToAdd (comma-separated). If none, use UpdateFoodPreferences with foodPreferences set to ""No specific restrictions"".
After saving, ask: ""What cuisine do you prefer? (e.g. Rajasthani, Punjabi, South Indian, Mediterranean)""
Do NOT skip the tool call.";
            }

            if (!completed.Contains("cuisine", StringComparer.OrdinalIgnoreCase))
            {
                return @"[ONBOARDING ACTIVE] The user is answering the CUISINE question.
ACTION REQUIRED: Extract their cuisine preference from the message.
You MUST call UpdateFoodPreferences with cuisineStyle set to their answer.
After saving, ask: ""What's your weekly workout split? Tell me what you'd like to focus on each day, or say 'use defaults'.""
Do NOT skip the tool call.";
            }

            if (!completed.Contains("schedule", StringComparer.OrdinalIgnoreCase))
            {
                return @"[ONBOARDING ACTIVE] The user is answering the WORKOUT SCHEDULE question.
ACTION REQUIRED: Extract their day-by-day workout schedule from the message.
You MUST call UpdateWorkoutSchedule with the appropriate day parameters (monday, tuesday, etc.).
If they say 'use defaults' or 'keep current', just acknowledge — no tool call needed.
After saving, tell them setup is complete and they're ready to go!
Do NOT skip the tool call.";
            }

            // All steps done — no injection needed
            return string.Empty;
        }

        private static async Task<string> ExecuteToolAsync(ChatAgentTools tools, string toolName, IReadOnlyDictionary<string, object?>? args)
        {
            return toolName switch
            {
                nameof(ChatAgentTools.GetUserProfile) => await tools.GetUserProfile(),
                nameof(ChatAgentTools.GetTodayDiary) => await tools.GetTodayDiary(),
                nameof(ChatAgentTools.GetHealthInsights) => await tools.GetHealthInsights(),
                nameof(ChatAgentTools.GetTodayPlans) => await tools.GetTodayPlans(),
                nameof(ChatAgentTools.GetRecentDiaryHistory) => await tools.GetRecentDiaryHistory(),
                nameof(ChatAgentTools.GetSleepDetails) => await tools.GetSleepDetails(),
                nameof(ChatAgentTools.GetExerciseHistory) => await tools.GetExerciseHistory(),
                nameof(ChatAgentTools.GetWeeklyWorkoutPlanHistory) => await tools.GetWeeklyWorkoutPlanHistory(),
                nameof(ChatAgentTools.GetWeeklyDietPlanHistory) => await tools.GetWeeklyDietPlanHistory(),
                nameof(ChatAgentTools.GetRecentFeedback) => await tools.GetRecentFeedback(),
                nameof(ChatAgentTools.GetWeeklyDigest) => await tools.GetWeeklyDigest(),
                nameof(ChatAgentTools.GetInBodyAnalysis) => await tools.GetInBodyAnalysis(),
                nameof(ChatAgentTools.GetNotifications) => await tools.GetNotifications(),
                nameof(ChatAgentTools.GetPlanGenerationStatus) => await tools.GetPlanGenerationStatus(),
                nameof(ChatAgentTools.UpdateFoodPreferences) => await tools.UpdateFoodPreferences(
                    GetArg(args, "excludedFoodsToAdd"),
                    GetArg(args, "excludedFoodsToRemove"),
                    GetArg(args, "cuisineStyle"),
                    GetArg(args, "cookingOilsToSet"),
                    GetArg(args, "stapleGrainsToSet"),
                    GetArg(args, "foodPreferences")),
                nameof(ChatAgentTools.UpdateConditions) => await tools.UpdateConditions(
                    GetArg(args, "conditions") ?? ""),
                nameof(ChatAgentTools.UpdateWorkoutSchedule) => await tools.UpdateWorkoutSchedule(
                    GetArg(args, "monday"), GetArg(args, "tuesday"), GetArg(args, "wednesday"),
                    GetArg(args, "thursday"), GetArg(args, "friday"), GetArg(args, "saturday"),
                    GetArg(args, "sunday")),
                nameof(ChatAgentTools.UpsertDiaryEntry) => await tools.UpsertDiaryEntry(
                    GetArg(args, "date"),
                    GetArg(args, "mealsJson"),
                    GetArg(args, "workoutLogJson"),
                    GetArg(args, "painLogJson"),
                    GetArgInt(args, "moodEnergy"),
                    GetArgDouble(args, "waterIntakeLitres"),
                    GetArg(args, "sleepNotes"),
                    GetArg(args, "generalNotes")),
                nameof(ChatAgentTools.SubmitPlanFeedback) => await tools.SubmitPlanFeedback(
                    GetArg(args, "planType") ?? "workout",
                    GetArgInt(args, "rating") ?? 3,
                    GetArg(args, "difficulty") ?? "just-right",
                    GetArg(args, "skippedItems"),
                    GetArg(args, "note")),
                nameof(ChatAgentTools.UpdatePersonalInfo) => await tools.UpdatePersonalInfo(
                    GetArg(args, "firstName"),
                    GetArg(args, "lastName"),
                    GetArgInt(args, "age"),
                    GetArg(args, "notificationTime")),
                nameof(ChatAgentTools.GetOnboardingStatus) => await tools.GetOnboardingStatus(),
                nameof(ChatAgentTools.GetFrequentMeals) => await tools.GetFrequentMeals(),
                _ => $"Unknown tool: {toolName}"
            };
        }

        private static string? GetArg(IReadOnlyDictionary<string, object?>? args, string key)
        {
            if (args is null || !args.TryGetValue(key, out var val) || val is null) return null;
            if (val is JsonElement je) return je.ValueKind == JsonValueKind.Null ? null : je.ToString();
            return val.ToString();
        }

        private static int? GetArgInt(IReadOnlyDictionary<string, object?>? args, string key)
        {
            var str = GetArg(args, key);
            return int.TryParse(str, out int v) ? v : null;
        }

        private static double? GetArgDouble(IReadOnlyDictionary<string, object?>? args, string key)
        {
            var str = GetArg(args, key);
            return double.TryParse(str, out double v) ? v : null;
        }

        private static string FormatToolDisplayName(string toolName)
        {
            return toolName switch
            {
                nameof(ChatAgentTools.GetUserProfile) => "Reading your profile",
                nameof(ChatAgentTools.GetTodayDiary) => "Checking today's diary",
                nameof(ChatAgentTools.GetHealthInsights) => "Analyzing health metrics",
                nameof(ChatAgentTools.GetTodayPlans) => "Loading today's plans",
                nameof(ChatAgentTools.GetRecentDiaryHistory) => "Reviewing diary history",
                nameof(ChatAgentTools.GetSleepDetails) => "Analyzing sleep stages",
                nameof(ChatAgentTools.GetExerciseHistory) => "Loading exercise sessions",
                nameof(ChatAgentTools.GetWeeklyWorkoutPlanHistory) => "Loading weekly workout plans",
                nameof(ChatAgentTools.GetWeeklyDietPlanHistory) => "Loading weekly diet plans",
                nameof(ChatAgentTools.GetRecentFeedback) => "Reviewing past feedback",
                nameof(ChatAgentTools.GetWeeklyDigest) => "Loading weekly digest",
                nameof(ChatAgentTools.GetInBodyAnalysis) => "Analyzing body composition",
                nameof(ChatAgentTools.GetNotifications) => "Checking notifications",
                nameof(ChatAgentTools.GetPlanGenerationStatus) => "Checking plan generation",
                nameof(ChatAgentTools.UpdateFoodPreferences) => "Updating food preferences",
                nameof(ChatAgentTools.UpdateConditions) => "Updating conditions",
                nameof(ChatAgentTools.UpdateWorkoutSchedule) => "Updating workout schedule",
                nameof(ChatAgentTools.UpsertDiaryEntry) => "Updating diary entry",
                nameof(ChatAgentTools.SubmitPlanFeedback) => "Submitting plan feedback",
                nameof(ChatAgentTools.UpdatePersonalInfo) => "Updating personal info",
                nameof(ChatAgentTools.GetOnboardingStatus) => "Checking onboarding status",
                nameof(ChatAgentTools.GetFrequentMeals) => "Loading frequent meals",
                _ => toolName
            };
        }
    }
}
