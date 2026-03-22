using FitnessAgentsWeb.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FitnessAgentsWeb.Controllers;

/// <summary>
/// API endpoints for in-app notifications: SSE stream, list, and mark-read.
/// </summary>
[Authorize]
[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly IAppNotificationStore _store;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public NotificationsController(IAppNotificationStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Server-Sent Events stream that pushes real-time notifications to the browser.
    /// Each connected client receives events as they arrive.
    /// </summary>
    [HttpGet("stream")]
    public async Task Stream(CancellationToken ct)
    {
        var userId = ResolveUserId();
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        // Send current unread count immediately on connect
        await SendUnreadCount(userId, ct);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _store.WaitForNotificationAsync(userId, ct);

                // New notification arrived — send the latest one and the updated count
                var latest = _store.GetForUser(userId);
                if (latest.Count > 0)
                {
                    var payload = JsonSerializer.Serialize(latest[0], JsonOptions);
                    await Response.WriteAsync($"event: notification\ndata: {payload}\n\n", ct);
                }

                await SendUnreadCount(userId, ct);
                await Response.Body.FlushAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Returns the list of recent notifications for the current user.
    /// </summary>
    [HttpGet]
    public IActionResult List()
    {
        var userId = ResolveUserId();
        var notifications = _store.GetForUser(userId);
        var unread = _store.GetUnreadCount(userId);
        return Ok(new { notifications, unreadCount = unread });
    }

    /// <summary>
    /// Marks a single notification as read.
    /// </summary>
    [HttpPost("{id}/read")]
    public IActionResult MarkRead(string id)
    {
        _store.MarkRead(id);
        return Ok();
    }

    /// <summary>
    /// Marks all notifications for the current user as read.
    /// </summary>
    [HttpPost("read-all")]
    public IActionResult MarkAllRead()
    {
        var userId = ResolveUserId();
        _store.MarkAllRead(userId);
        return Ok();
    }

    private string ResolveUserId() =>
        User.Identity?.Name ?? "default_user";

    private async Task SendUnreadCount(string userId, CancellationToken ct)
    {
        var count = _store.GetUnreadCount(userId);
        await Response.WriteAsync($"event: unread\ndata: {{\"count\":{count}}}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }
}
