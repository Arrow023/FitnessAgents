using FitnessAgentsWeb.Core.Configuration;
using FitnessAgentsWeb.Core.Helpers;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessAgentsWeb.Controllers;

[Authorize]
public class DietController : Controller
{
    private readonly IStorageRepository _storageRepository;
    private readonly IAiOrchestratorService _orchestrator;
    private readonly IAppConfigurationProvider _configProvider;

    public DietController(IStorageRepository storageRepository, IAiOrchestratorService orchestrator, IAppConfigurationProvider configProvider)
    {
        _storageRepository = storageRepository;
        _orchestrator = orchestrator;
        _configProvider = configProvider;
    }

    public async Task<IActionResult> Index(string? userId = null)
    {
        userId = ResolveUserId(userId);
        ViewData["ActiveNav"] = "diet";

        var dietTask = _storageRepository.GetLatestDietAsync(userId);
        var historyTask = _storageRepository.GetWeeklyDietHistoryAsync(userId);
        await Task.WhenAll(dietTask, historyTask);

        // Only show latest diet as "today's" if it was actually generated today
        var latestDiet = dietTask.Result;
        var appNow = TimezoneHelper.GetAppNow(_configProvider.GetAppTimezone());
        if (latestDiet != null && latestDiet.PlanDate.Date != appNow.Date)
            latestDiet = null;

        var model = new DietListViewModel
        {
            UserId = userId,
            LatestDiet = latestDiet,
            DietHistory = historyTask.Result
        };

        return View(model);
    }

    public async Task<IActionResult> Detail(string userId, string day)
    {
        userId = ResolveUserId(userId);
        ViewData["ActiveNav"] = "diet";

        var dietHistory = await _storageRepository.GetWeeklyDietHistoryAsync(userId);
        Models.DietPlan? diet = null;

        if (dietHistory is not null && dietHistory.PastDiets.TryGetValue(day, out var plan))
        {
            diet = plan;
        }
        else
        {
            diet = await _storageRepository.GetLatestDietAsync(userId);
        }

        if (diet is null)
        {
            return RedirectToAction("Index", new { userId });
        }

        var model = new DietDetailViewModel
        {
            UserId = userId,
            DayOfWeek = day,
            Diet = diet
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> ResendEmail(string userId, string? dayOfWeek = null)
    {
        userId = ResolveUserId(userId);
        var dietHistory = await _storageRepository.GetWeeklyDietHistoryAsync(userId);
        Models.DietPlan? diet = null;

        if (!string.IsNullOrEmpty(dayOfWeek) && dietHistory is not null && dietHistory.PastDiets.TryGetValue(dayOfWeek, out var plan))
        {
            diet = plan;
        }
        else
        {
            diet = await _storageRepository.GetLatestDietAsync(userId);
        }

        if (diet is not null)
        {
            await _orchestrator.EmailStoreDietPlanAsync(userId, diet);
        }

        return RedirectToAction("Index", new { userId });
    }

    [HttpGet]
    public async Task<IActionResult> Feedback(string userId, string day)
    {
        userId = ResolveUserId(userId);
        ViewData["ActiveNav"] = "diet";

        string planId = $"{userId}_{day}_diet";
        var existing = await _storageRepository.GetPlanFeedbackAsync(userId, planId);

        var model = new PlanFeedbackViewModel
        {
            UserId = userId,
            DayOfWeek = day,
            PlanType = "diet",
            ExistingFeedback = existing
        };

        return View("Feedback", model);
    }

    [HttpPost]
    public async Task<IActionResult> SubmitFeedback(PlanFeedbackViewModel model)
    {
        model.UserId = ResolveUserId(model.UserId);

        var feedback = new Models.PlanFeedback
        {
            PlanId = $"{model.UserId}_{model.DayOfWeek}_diet",
            PlanType = "diet",
            FeedbackDate = DateTime.UtcNow,
            Rating = model.Rating,
            Difficulty = model.Difficulty ?? "just-right",
            SkippedItems = string.IsNullOrEmpty(model.SkippedItems)
                ? []
                : model.SkippedItems.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            Note = model.Note ?? string.Empty
        };

        await _storageRepository.SavePlanFeedbackAsync(model.UserId, feedback);
        return RedirectToAction("Detail", new { userId = model.UserId, day = model.DayOfWeek });
    }

    /// <summary>
    /// API endpoint for inline quick feedback (thumbs up/down).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> QuickFeedback([FromBody] QuickFeedbackRequest request)
    {
        string userId = ResolveUserId(request?.UserId);

        var feedback = new Models.PlanFeedback
        {
            PlanId = $"diet_{DateTime.UtcNow:yyyy-MM-dd}",
            PlanType = "diet",
            FeedbackDate = DateTime.UtcNow,
            Rating = Math.Clamp(request?.Rating ?? 3, 1, 5),
            Difficulty = request?.Difficulty ?? "just-right",
            Note = request?.Note ?? string.Empty
        };

        await _storageRepository.SavePlanFeedbackAsync(userId, feedback);
        return Json(new { success = true });
    }

    public sealed class QuickFeedbackRequest
    {
        public string? UserId { get; set; }
        public int Rating { get; set; }
        public string? Difficulty { get; set; }
        public string? Note { get; set; }
    }

    private string ResolveUserId(string? userId)
    {
        if (User.IsInRole("User"))
            return User.Identity?.Name ?? "default_user";

        return string.IsNullOrEmpty(userId) ? User.Identity?.Name ?? "default_user" : userId;
    }
}
