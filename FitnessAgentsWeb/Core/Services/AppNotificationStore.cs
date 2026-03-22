using System.Collections.Concurrent;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models;

namespace FitnessAgentsWeb.Core.Services;

/// <summary>
/// Thread-safe in-memory notification store with per-user event signaling for SSE.
/// Auto-prunes notifications older than 24 hours.
/// </summary>
public class AppNotificationStore : IAppNotificationStore
{
    private readonly ConcurrentDictionary<string, List<AppNotification>> _store = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _signals = new();
    private static readonly TimeSpan Retention = TimeSpan.FromHours(24);
    private DateTime _lastCleanup = DateTime.UtcNow;

    /// <inheritdoc />
    public void Push(AppNotification notification)
    {
        var list = _store.GetOrAdd(notification.UserId, _ => []);
        lock (list)
        {
            list.Add(notification);
        }

        // Signal any SSE listener waiting for this user
        var signal = _signals.GetOrAdd(notification.UserId, _ => new SemaphoreSlim(0, int.MaxValue));
        signal.Release();

        CleanupIfDue();
    }

    /// <inheritdoc />
    public IReadOnlyList<AppNotification> GetForUser(string userId)
    {
        if (!_store.TryGetValue(userId, out var list)) return [];
        lock (list)
        {
            return list.OrderByDescending(n => n.CreatedAt).Take(50).ToList();
        }
    }

    /// <inheritdoc />
    public int GetUnreadCount(string userId)
    {
        if (!_store.TryGetValue(userId, out var list)) return 0;
        lock (list)
        {
            return list.Count(n => !n.IsRead);
        }
    }

    /// <inheritdoc />
    public void MarkRead(string notificationId)
    {
        foreach (var list in _store.Values)
        {
            lock (list)
            {
                var item = list.FirstOrDefault(n => n.Id == notificationId);
                if (item is not null)
                {
                    item.IsRead = true;
                    return;
                }
            }
        }
    }

    /// <inheritdoc />
    public void MarkAllRead(string userId)
    {
        if (!_store.TryGetValue(userId, out var list)) return;
        lock (list)
        {
            foreach (var n in list)
            {
                n.IsRead = true;
            }
        }
    }

    /// <inheritdoc />
    public async Task WaitForNotificationAsync(string userId, CancellationToken ct)
    {
        var signal = _signals.GetOrAdd(userId, _ => new SemaphoreSlim(0, int.MaxValue));
        await signal.WaitAsync(ct);
    }

    private void CleanupIfDue()
    {
        if (DateTime.UtcNow - _lastCleanup < TimeSpan.FromMinutes(10)) return;
        _lastCleanup = DateTime.UtcNow;
        var cutoff = DateTime.UtcNow - Retention;

        foreach (var list in _store.Values)
        {
            lock (list)
            {
                list.RemoveAll(n => n.CreatedAt < cutoff);
            }
        }
    }
}
