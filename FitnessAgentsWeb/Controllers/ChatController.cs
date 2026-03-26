using FitnessAgentsWeb.Core.Helpers;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Core.Services;
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
        private readonly IStorageRepository _storage;
        private readonly MealVisionService _mealVision;
        private readonly ILogger<ChatController> _logger;

        public ChatController(IChatAgentService chatAgent, IStorageRepository storage, MealVisionService mealVision, ILogger<ChatController> logger)
        {
            _chatAgent = chatAgent;
            _storage = storage;
            _mealVision = mealVision;
            _logger = logger;
        }

        /// <summary>
        /// Serves the chat page. Passes onboarding status to the view.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Chat";
            ViewData["ActiveNav"] = "chat";

            string userId = ResolveUserId();
            var profile = await _storage.GetUserProfileAsync(userId);
            ViewBag.NeedsOnboarding = profile is null || !profile.IsOnboardingComplete;

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

        /// <summary>
        /// Returns the user's onboarding status as JSON.
        /// </summary>
        [HttpGet("/api/chat/onboarding-status")]
        public async Task<IActionResult> OnboardingStatus()
        {
            string userId = ResolveUserId();
            var profile = await _storage.GetUserProfileAsync(userId);

            return Json(new
            {
                needsOnboarding = profile is null || !profile.IsOnboardingComplete,
                completedSteps = profile?.OnboardingCompleted ?? new List<string>()
            });
        }

        /// <summary>
        /// Accepts a meal photo, extracts food items via Vision LLM, returns structured data.
        /// </summary>
        [HttpPost("/api/chat/meal-photo")]
        public async Task<IActionResult> AnalyzeMealPhoto(IFormFile photo)
        {
            if (photo is null || photo.Length == 0)
                return BadRequest(new { error = "No photo provided." });

            if (photo.Length > 10 * 1024 * 1024) // 10MB limit
                return BadRequest(new { error = "Photo too large. Maximum 10MB." });

            string mimeType = photo.ContentType ?? "image/jpeg";
            if (!mimeType.StartsWith("image/"))
                return BadRequest(new { error = "File must be an image." });

            try
            {
                using var stream = photo.OpenReadStream();
                var result = await _mealVision.ExtractMealFromImageAsync(stream, mimeType);
                _logger.LogInformation("[MealVision] Extracted {ItemCount} items from photo for user {UserId}", result.Items.Count, ResolveUserId());
                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[MealVision] Failed to analyze meal photo");
                return StatusCode(500, new { error = "Failed to analyze the photo. Please try again." });
            }
        }
    }
}
