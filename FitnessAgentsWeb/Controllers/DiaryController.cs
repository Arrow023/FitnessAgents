using FitnessAgentsWeb.Core.Helpers;
using FitnessAgentsWeb.Core.Configuration;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessAgentsWeb.Controllers;

[Authorize]
public class DiaryController : Controller
{
    private readonly IStorageRepository _storageRepository;
    private readonly IAppConfigurationProvider _configProvider;

    public DiaryController(IStorageRepository storageRepository, IAppConfigurationProvider configProvider)
    {
        _storageRepository = storageRepository;
        _configProvider = configProvider;
    }

    public async Task<IActionResult> Index(string? userId = null, string? date = null)
    {
        userId = ResolveUserId(userId);
        ViewData["ActiveNav"] = "diary";

        var tzId = _configProvider.GetAppTimezone();
        var appNow = TimezoneHelper.GetAppNow(tzId);
        string todayDate = appNow.ToString("yyyy-MM-dd");

        // Use requested date or default to today; clamp to today max
        string selectedDate = todayDate;
        if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var parsed))
        {
            if (parsed.Date <= appNow.Date)
                selectedDate = parsed.ToString("yyyy-MM-dd");
        }

        bool isToday = selectedDate == todayDate;

        var entry = await _storageRepository.GetDiaryEntryAsync(userId, selectedDate);
        var recentEntries = await _storageRepository.GetRecentDiaryEntriesAsync(userId, 14);

        // Load diet plan only for today
        var todayDiet = isToday ? await _storageRepository.GetLatestDietAsync(userId) : null;

        // Load workout plan for the selected date's day of week (structured JSON)
        var selectedDt = DateTime.TryParse(selectedDate, out var sdp) ? sdp : appNow;
        var dayName = selectedDt.DayOfWeek.ToString();
        WorkoutPlan? workoutPlan = null;
        string? workoutHtml = null;

        var workoutHistory = await _storageRepository.GetWeeklyHistoryAsync(userId);
        if (workoutHistory != null)
        {
            // Prefer structured plan; fall back to markdown rendering for old data
            if (workoutHistory.PastWorkoutPlans.TryGetValue(dayName, out var plan))
            {
                workoutPlan = plan;
            }

            if (workoutHistory.PastWorkouts.TryGetValue(dayName, out var workoutMd))
            {
                // Strip diet portion for display (everything after ---\n)
                var sepIdx = workoutMd.IndexOf("\n---\n", StringComparison.Ordinal);
                var cleanMd = sepIdx > 0 ? workoutMd[..sepIdx] : workoutMd;
                workoutHtml = MarkdownStylingHelper.RenderToEmailHtml(cleanMd);
            }
        }

        ViewBag.TodayDate = selectedDate;
        ViewBag.ActualToday = todayDate;
        ViewBag.TodayFormatted = DateTime.TryParse(selectedDate, out var dt)
            ? dt.ToString("dddd, MMM dd") : selectedDate;
        ViewBag.IsToday = isToday;
        ViewBag.UserId = userId;
        ViewBag.TodayDiet = todayDiet;
        ViewBag.RecentEntries = recentEntries;
        ViewBag.WorkoutHtml = workoutHtml;
        ViewBag.WorkoutPlan = workoutPlan;
        ViewBag.DayName = dayName;

        return View(entry ?? new DailyDiary { Date = selectedDate });
    }

    [HttpPost]
    public async Task<IActionResult> Save(string userId, string date,
        string mealTimes, string mealFoods, string mealQuantities, string mealFromPlan, string mealSubstitutions,
        string workoutExercises, string workoutCompleted, string workoutFeelings, string workoutNotes,
        string painAreas, string painSeverities, string painDescriptions,
        int moodEnergy, double waterIntake, string sleepNotes, string generalNotes)
    {
        userId = ResolveUserId(userId);

        var entry = new DailyDiary
        {
            Date = date,
            MoodEnergy = moodEnergy,
            WaterIntakeLitres = waterIntake,
            SleepNotes = sleepNotes ?? string.Empty,
            GeneralNotes = generalNotes ?? string.Empty,
            ActualMeals = ParseMeals(mealTimes, mealFoods, mealQuantities, mealFromPlan, mealSubstitutions),
            WorkoutLog = ParseWorkoutLog(workoutExercises, workoutCompleted, workoutFeelings, workoutNotes),
            PainLog = ParsePainLog(painAreas, painSeverities, painDescriptions)
        };

        await _storageRepository.SaveDiaryEntryAsync(userId, entry);

        TempData["DiarySuccess"] = "Diary saved successfully!";
        return RedirectToAction("Index", new { userId, date });
    }

    [HttpGet]
    public async Task<IActionResult> Entry(string userId, string date)
    {
        userId = ResolveUserId(userId);
        ViewData["ActiveNav"] = "diary";

        var entry = await _storageRepository.GetDiaryEntryAsync(userId, date);
        ViewBag.TodayDate = date;
        ViewBag.TodayFormatted = DateTime.TryParse(date, out var dt) ? dt.ToString("dddd, MMM dd") : date;
        ViewBag.UserId = userId;
        ViewBag.RecentEntries = await _storageRepository.GetRecentDiaryEntriesAsync(userId, 7);

        return View("Index", entry ?? new DailyDiary { Date = date });
    }

    private static List<DiaryMeal> ParseMeals(string? times, string? foods, string? quantities, string? fromPlan, string? substitutions)
    {
        var meals = new List<DiaryMeal>();
        if (string.IsNullOrEmpty(times) || string.IsNullOrEmpty(foods)) return meals;

        var t = times.Split('|');
        var f = foods.Split('|');
        var q = (quantities ?? "").Split('|');
        var p = (fromPlan ?? "").Split('|');
        var s = (substitutions ?? "").Split('|');

        for (int i = 0; i < t.Length && i < f.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(f[i])) continue;
            meals.Add(new DiaryMeal
            {
                MealTime = i < t.Length ? t[i].Trim() : "",
                FoodName = f[i].Trim(),
                Quantity = i < q.Length ? q[i].Trim() : "",
                WasFromPlan = i < p.Length && p[i].Trim() == "true",
                Substitution = i < s.Length ? s[i].Trim() : ""
            });
        }
        return meals;
    }

    private static List<DiaryWorkoutLog> ParseWorkoutLog(string? exercises, string? completed, string? feelings, string? notes)
    {
        var logs = new List<DiaryWorkoutLog>();
        if (string.IsNullOrEmpty(exercises)) return logs;

        var e = exercises.Split('|');
        var c = (completed ?? "").Split('|');
        var f = (feelings ?? "").Split('|');
        var n = (notes ?? "").Split('|');

        for (int i = 0; i < e.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(e[i])) continue;
            logs.Add(new DiaryWorkoutLog
            {
                Exercise = e[i].Trim(),
                Completed = i < c.Length && c[i].Trim() == "true",
                Feeling = i < f.Length ? f[i].Trim() : "",
                Notes = i < n.Length ? n[i].Trim() : ""
            });
        }
        return logs;
    }

    private static List<DiaryPainLog> ParsePainLog(string? areas, string? severities, string? descriptions)
    {
        var logs = new List<DiaryPainLog>();
        if (string.IsNullOrEmpty(areas)) return logs;

        var a = areas.Split('|');
        var s = (severities ?? "").Split('|');
        var d = (descriptions ?? "").Split('|');

        for (int i = 0; i < a.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(a[i])) continue;
            logs.Add(new DiaryPainLog
            {
                BodyArea = a[i].Trim(),
                Severity = i < s.Length && int.TryParse(s[i].Trim(), out var sev) ? sev : 1,
                Description = i < d.Length ? d[i].Trim() : ""
            });
        }
        return logs;
    }

    private string ResolveUserId(string? userId)
    {
        if (User.IsInRole("User"))
            return User.Identity?.Name ?? "default_user";
        return string.IsNullOrEmpty(userId) ? User.Identity?.Name ?? "default_user" : userId;
    }
}
