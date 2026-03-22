using System.Text.Json.Serialization;

namespace FitnessAgentsWeb.Models;

/// <summary>
/// Represents an in-app notification shown in the bell dropdown.
/// </summary>
public class AppNotification
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public required string UserId { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public required NotificationType Type { get; set; }
    public string? Icon { get; set; }
    public string? Link { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
}

/// <summary>
/// Categories of in-app notifications.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationType
{
    PlanRequested,
    WorkoutReady,
    DietReady,
    HealthDataReceived,
    Error
}
