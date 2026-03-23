namespace FitnessAgentsWeb.Models
{
    /// <summary>
    /// Represents a single message in a chat conversation history.
    /// </summary>
    public class ChatHistoryMessage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
        public string Role { get; set; } = "user"; // user, assistant, system
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// SSE event types streamed to the chat UI during agent processing.
    /// </summary>
    public enum ChatStreamEventType
    {
        Thinking,
        ToolCall,
        ToolResult,
        Message,
        Error,
        Done
    }

    /// <summary>
    /// Represents a single SSE event sent during chat streaming.
    /// </summary>
    public class ChatStreamEvent
    {
        public ChatStreamEventType Type { get; set; }
        public string? Text { get; set; }
        public string? ToolName { get; set; }
        public string? ToolArgs { get; set; }
        public string? ToolResult { get; set; }
    }
}
