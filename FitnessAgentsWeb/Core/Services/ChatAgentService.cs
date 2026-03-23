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

        public ChatAgentService(
            IAppConfigurationProvider configProvider,
            IStorageRepository storage,
            IHealthDataProcessor healthProcessor,
            ILogger<ChatAgentService> logger)
            : base(configProvider, logger)
        {
            _storage = storage;
            _healthProcessor = healthProcessor;
        }

        public async IAsyncEnumerable<ChatStreamEvent> ProcessMessageAsync(
            string userId,
            string userMessage,
            List<ChatHistoryMessage> conversationHistory,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var tools = new ChatAgentTools(_storage, _healthProcessor, userId, _logger);
            IChatClient chatClient = GetChatClient();

            var chatOptions = new ChatOptions
            {
                Tools = BuildToolDefinitions(tools),
                Temperature = 0.7f
            };

            var messages = BuildMessageHistory(conversationHistory, userMessage, userId);

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
                AIFunctionFactory.Create(tools.UpdateFoodPreferences),
                AIFunctionFactory.Create(tools.UpdateConditions),
                AIFunctionFactory.Create(tools.UpdateWorkoutSchedule),
                AIFunctionFactory.Create(tools.UpsertDiaryEntry),
                AIFunctionFactory.Create(tools.SubmitPlanFeedback),
            ];
        }

        private List<Microsoft.Extensions.AI.ChatMessage> BuildMessageHistory(List<ChatHistoryMessage> history, string userMessage, string userId)
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

TOOL USAGE RULES:
1. Before any UPDATE operation, call the corresponding GET tool first.
2. For profile changes: GetUserProfile → then UpdateFoodPreferences / UpdateConditions / UpdateWorkoutSchedule
3. For diary entries: GetTodayDiary → then UpsertDiaryEntry (merges, never replaces)
4. For health questions: GetHealthInsights and/or GetTodayPlans
5. For pattern analysis: GetRecentDiaryHistory
6. When user provides plan feedback: SubmitPlanFeedback

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

            messages.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, userMessage));
            return messages;
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
                nameof(ChatAgentTools.UpdateFoodPreferences) => "Updating food preferences",
                nameof(ChatAgentTools.UpdateConditions) => "Updating conditions",
                nameof(ChatAgentTools.UpdateWorkoutSchedule) => "Updating workout schedule",
                nameof(ChatAgentTools.UpsertDiaryEntry) => "Updating diary entry",
                nameof(ChatAgentTools.SubmitPlanFeedback) => "Submitting plan feedback",
                _ => toolName
            };
        }
    }
}
