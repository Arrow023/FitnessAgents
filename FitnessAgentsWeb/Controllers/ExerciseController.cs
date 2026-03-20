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
                        EndTime = TimeZoneInfo.ConvertTimeFromUtc(e.EndTime, tz).ToString("hh:mm tt")
                    }).ToList()
                })
                .ToList();

            model.Days = grouped;
            model.TotalSessions = exercises.Count;
            model.TotalMinutes = exercises.Sum(e => e.DurationSeconds) / 60;
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
}
