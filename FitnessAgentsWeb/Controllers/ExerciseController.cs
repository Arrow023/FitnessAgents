using FitnessAgentsWeb.Core.Helpers;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessAgentsWeb.Controllers;

[Authorize]
public class ExerciseController : Controller
{
    private readonly IStorageRepository _storageRepository;

    public ExerciseController(IStorageRepository storageRepository)
    {
        _storageRepository = storageRepository;
    }

    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Index(string? userId = null)
    {
        userId = ResolveUserId(userId);
        ViewData["ActiveNav"] = "exercise";

        var tz = ResolveTimeZone();
        var healthData = await _storageRepository.GetTodayHealthDataAsync(userId);

        var model = new ExerciseHistoryViewModel { UserId = userId };

        if (healthData?.Exercise is { Count: > 0 } exercises)
        {
            var todayLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).Date;

            var grouped = exercises
                .OrderByDescending(e => e.StartTime)
                .GroupBy(e => TimeZoneInfo.ConvertTimeFromUtc(e.StartTime, tz).Date)
                .Select(g => new ExerciseDayGroup
                {
                    DateLabel = FormatDateLabel(g.Key, todayLocal),
                    IsToday = g.Key == todayLocal,
                    Sessions = g.Select(e => new ExerciseSessionItem
                    {
                        TypeCode = e.Type,
                        DurationMinutes = e.DurationSeconds / 60,
                        StartTime = TimeZoneInfo.ConvertTimeFromUtc(e.StartTime, tz).ToString("hh:mm tt"),
                        EndTime = TimeZoneInfo.ConvertTimeFromUtc(e.EndTime, tz).ToString("hh:mm tt"),
                        StartTimeUtc = e.StartTime,
                        EndTimeUtc = e.EndTime
                    }).ToList()
                })
                .ToList();

            model.Days = grouped;
            model.TotalSessions = exercises.Count;
            model.TotalMinutes = exercises.Sum(e => e.DurationSeconds) / 60;
        }

        return View(model);
    }

    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Detail(string startUtc, string? userId = null)
    {
        userId = ResolveUserId(userId);
        ViewData["ActiveNav"] = "exercise";

        if (!DateTime.TryParse(startUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var startTimeUtc))
            return RedirectToAction("Index");

        var tz = ResolveTimeZone();
        var healthData = await _storageRepository.GetTodayHealthDataAsync(userId);

        if (healthData?.Exercise is not { Count: > 0 } exercises)
            return RedirectToAction("Index");

        // Find the matching exercise session
        var exercise = exercises.FirstOrDefault(e => Math.Abs((e.StartTime - startTimeUtc).TotalSeconds) < 2);
        if (exercise == null)
            return RedirectToAction("Index");

        var sessionStart = exercise.StartTime;
        var sessionEnd = exercise.EndTime;

        var model = new ExerciseDetailViewModel
        {
            UserId = userId,
            TypeCode = exercise.Type,
            Name = ExerciseTypeHelper.GetExerciseName(exercise.Type),
            Icon = ExerciseTypeHelper.GetExerciseIcon(exercise.Type),
            DurationMinutes = exercise.DurationSeconds / 60,
            StartTime = TimeZoneInfo.ConvertTimeFromUtc(sessionStart, tz).ToString("h:mm tt"),
            EndTime = TimeZoneInfo.ConvertTimeFromUtc(sessionEnd, tz).ToString("h:mm tt")
        };

        // Correlate steps overlapping with this session (proportional)
        if (healthData.Steps is { Count: > 0 })
        {
            model.Steps = (int)healthData.Steps
                .Select(s => ProportionalValue(s.StartTime, s.EndTime, sessionStart, sessionEnd, s.Count))
                .Sum();
        }

        // Correlate distance (proportional)
        if (healthData.Distance is { Count: > 0 })
        {
            var meters = healthData.Distance
                .Select(d => ProportionalValue(d.StartTime, d.EndTime, sessionStart, sessionEnd, d.Meters))
                .Sum();
            if (meters > 0)
            {
                model.DistanceKm = Math.Round(meters / 1000.0, 2);
                if (model.DurationMinutes > 0 && model.DistanceKm > 0)
                    model.PaceMinPerKm = Math.Round(model.DurationMinutes / model.DistanceKm.Value, 2);
            }
        }

        // Correlate calories (proportional)
        if (healthData.ActiveCalories is { Count: > 0 })
        {
            model.ActiveCalories = Math.Round(healthData.ActiveCalories
                .Select(c => ProportionalValue(c.StartTime, c.EndTime, sessionStart, sessionEnd, c.Calories))
                .Sum());
        }

        if (healthData.TotalCalories is { Count: > 0 })
        {
            model.TotalCalories = Math.Round(healthData.TotalCalories
                .Select(c => ProportionalValue(c.StartTime, c.EndTime, sessionStart, sessionEnd, c.Calories))
                .Sum());
        }

        // Correlate heart rate
        if (healthData.HeartRate is { Count: > 0 })
        {
            var hrDuringSession = healthData.HeartRate
                .Where(h => h.Time >= sessionStart && h.Time <= sessionEnd)
                .OrderBy(h => h.Time)
                .ToList();

            if (hrDuringSession.Count > 0)
            {
                model.AvgHeartRate = (int)Math.Round(hrDuringSession.Average(h => h.Bpm));
                model.MinHeartRate = hrDuringSession.Min(h => h.Bpm);
                model.MaxHeartRate = hrDuringSession.Max(h => h.Bpm);

                model.HeartRateTimeline = hrDuringSession.Select(h => new HeartRatePoint
                {
                    Time = TimeZoneInfo.ConvertTimeFromUtc(h.Time, tz).ToString("h:mm tt"),
                    Bpm = h.Bpm
                }).ToList();

                // Calculate heart rate zones (using standard zones based on estimated max HR of 220 - age, default 190)
                var maxHr = 190;
                var zones = new[]
                {
                    (Label: "Rest", Min: 0.0, Max: 0.50, Color: "#94a3b8"),
                    (Label: "Zone 1", Min: 0.50, Max: 0.60, Color: "#60a5fa"),
                    (Label: "Zone 2", Min: 0.60, Max: 0.70, Color: "#34d399"),
                    (Label: "Zone 3", Min: 0.70, Max: 0.80, Color: "#fbbf24"),
                    (Label: "Zone 4", Min: 0.80, Max: 0.90, Color: "#fb923c"),
                    (Label: "Zone 5", Min: 0.90, Max: 1.10, Color: "#f87171")
                };

                var totalPoints = hrDuringSession.Count;
                model.HeartRateZones = zones.Select(z =>
                {
                    var count = hrDuringSession.Count(h => h.Bpm >= maxHr * z.Min && h.Bpm < maxHr * z.Max);
                    return new HeartRateZone
                    {
                        Label = z.Label,
                        Range = $"{(int)(maxHr * z.Min)}-{(int)(maxHr * z.Max)} bpm",
                        Percentage = totalPoints > 0 ? Math.Round(100.0 * count / totalPoints) : 0,
                        Color = z.Color
                    };
                }).ToList();

                // Simple workout score: weighted zones (more time in higher zones = higher score)
                var weights = new[] { 0.0, 0.3, 0.5, 0.7, 0.9, 1.0 };
                var weightedScore = model.HeartRateZones
                    .Select((z, i) => z.Percentage / 100.0 * weights[i])
                    .Sum();
                model.WorkoutScore = (int)Math.Round(weightedScore * 100);
            }
        }

        return View(model);
    }

    private static string FormatDateLabel(DateTime date, DateTime today)
    {
        if (date == today) return "Today";
        if (date == today.AddDays(-1)) return "Yesterday";
        return date.ToString("ddd, MMM dd");
    }

    private static TimeZoneInfo ResolveTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(TimezoneHelper.CurrentTimezoneId); }
        catch
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
            catch { return TimeZoneInfo.Local; }
        }
    }

    private string ResolveUserId(string? userId)
    {
        if (User.IsInRole("User"))
            return User.Identity?.Name ?? "default_user";

        return string.IsNullOrEmpty(userId) ? User.Identity?.Name ?? "default_user" : userId;
    }

    /// <summary>
    /// Calculates the proportional share of a ranged metric that overlaps with the exercise session.
    /// E.g. if a step record covers 6 AM–6 PM (12 hrs) with 20k steps, and the exercise is 6:30–6:35 PM (5 min),
    /// only 5/720 of the steps (~139) are attributed to the session.
    /// </summary>
    private static double ProportionalValue(DateTime recordStart, DateTime recordEnd, DateTime sessionStart, DateTime sessionEnd, double totalValue)
    {
        var overlapStart = recordStart > sessionStart ? recordStart : sessionStart;
        var overlapEnd = recordEnd < sessionEnd ? recordEnd : sessionEnd;

        if (overlapEnd <= overlapStart)
            return 0;

        var recordDuration = (recordEnd - recordStart).TotalSeconds;
        if (recordDuration <= 0)
            return 0;

        var overlapDuration = (overlapEnd - overlapStart).TotalSeconds;
        return totalValue * (overlapDuration / recordDuration);
    }
}
