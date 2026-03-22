using FitnessAgentsWeb.Models;

namespace FitnessAgentsWeb.Core.Interfaces;

/// <summary>
/// In-memory store for user-scoped in-app notifications.
/// </summary>
public interface IAppNotificationStore
{
    /// <summary>
    /// Pushes a new notification for a user and notifies any active SSE listeners.
    /// </summary>
    void Push(AppNotification notification);

    /// <summary>
    /// Returns all notifications for a user, most recent first.
    /// </summary>
    IReadOnlyList<AppNotification> GetForUser(string userId);

    /// <summary>
    /// Returns the count of unread notifications for a user.
    /// </summary>
    int GetUnreadCount(string userId);

    /// <summary>
    /// Marks a specific notification as read.
    /// </summary>
    void MarkRead(string notificationId);

    /// <summary>
    /// Marks all notifications for a user as read.
    /// </summary>
    void MarkAllRead(string userId);

    /// <summary>
    /// Waits asynchronously until a new notification arrives for the user or cancellation is requested.
    /// Used by SSE endpoints to avoid polling.
    /// </summary>
    Task WaitForNotificationAsync(string userId, CancellationToken ct);
}
