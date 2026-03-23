using FitnessAgentsWeb.Core.Helpers;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FitnessAgentsWeb.Controllers
{
    /// <summary>
    /// Controller for the AI chat assistant — serves the chat view and SSE streaming endpoint.
    /// </summary>
    [Authorize]
    public class ChatController : Controller
    {
        private readonly IChatAgentService _chatAgent;
        private readonly ILogger<ChatController> _logger;

        public ChatController(IChatAgentService chatAgent, ILogger<ChatController> logger)
        {
            _chatAgent = chatAgent;
            _logger = logger;
        }

        /// <summary>
        /// Serves the dedicated chat page.
        /// </summary>
        [HttpGet]
        public IActionResult Index()
        {
            ViewData["Title"] = "Chat";
            ViewData["ActiveNav"] = "chat";
            return View();
        }

        /// <summary>
        /// SSE streaming endpoint — receives a chat message and streams back events.
        /// </summary>
        [HttpPost("/api/chat/stream")]
        public async Task StreamChat()
        {
            Response.ContentType = "text/event-stream";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            string userId = ResolveUserId();

            string body;
            using (var reader = new StreamReader(Request.Body))
                body = await reader.ReadToEndAsync();

            ChatRequest? request;
            try
            {
                request = JsonSerializer.Deserialize<ChatRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                await WriteSseEvent("error", new { text = "Invalid request format." });
                return;
            }

            if (request is null || string.IsNullOrWhiteSpace(request.Message))
            {
                await WriteSseEvent("error", new { text = "Message cannot be empty." });
                return;
            }

            _logger.LogInformation("[ChatAgent] User {UserId} sent: {Message}", userId, request.Message[..Math.Min(request.Message.Length, 100)]);

            try
            {
                await foreach (var evt in _chatAgent.ProcessMessageAsync(userId, request.Message, request.History ?? [], HttpContext.RequestAborted))
                {
                    string eventType = evt.Type switch
                    {
                        ChatStreamEventType.Thinking => "thinking",
                        ChatStreamEventType.ToolCall => "tool_call",
                        ChatStreamEventType.ToolResult => "tool_result",
                        ChatStreamEventType.Message => "message",
                        ChatStreamEventType.Error => "error",
                        ChatStreamEventType.Done => "done",
                        _ => "message"
                    };

                    object data = evt.Type switch
                    {
                        ChatStreamEventType.Thinking => new { text = evt.Text },
                        ChatStreamEventType.ToolCall => new { name = evt.ToolName, args = evt.ToolArgs },
                        ChatStreamEventType.ToolResult => new { name = evt.ToolName, result = evt.ToolResult },
                        ChatStreamEventType.Message => new { text = evt.Text, html = MarkdownStylingHelper.RenderToWebHtml(evt.Text ?? "") },
                        ChatStreamEventType.Error => new { text = evt.Text },
                        ChatStreamEventType.Done => new { },
                        _ => new { text = evt.Text }
                    };

                    await WriteSseEvent(eventType, data);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("[ChatAgent] Stream cancelled for user {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ChatAgent] Stream error for user {UserId}", userId);
                await WriteSseEvent("error", new { text = "An unexpected error occurred." });
            }
        }

        private async Task WriteSseEvent(string eventType, object data)
        {
            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await Response.WriteAsync($"event: {eventType}\ndata: {json}\n\n");
            await Response.Body.FlushAsync();
        }

        private string ResolveUserId()
        {
            if (User.IsInRole("Admin"))
            {
                string? requested = Request.Query["userId"];
                if (!string.IsNullOrEmpty(requested)) return requested;
            }
            return User.Identity?.Name ?? "unknown";
        }

        private sealed class ChatRequest
        {
            public string Message { get; set; } = string.Empty;
            public List<ChatHistoryMessage>? History { get; set; }
        }
    }
}
