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

    public DietController(IStorageRepository storageRepository, IAiOrchestratorService orchestrator)
    {
        _storageRepository = storageRepository;
        _orchestrator = orchestrator;
    }

    public async Task<IActionResult> Index(string? userId = null)
    {
        userId = ResolveUserId(userId);
        ViewData["ActiveNav"] = "diet";

        var dietTask = _storageRepository.GetLatestDietAsync(userId);
        var historyTask = _storageRepository.GetWeeklyDietHistoryAsync(userId);
        await Task.WhenAll(dietTask, historyTask);

        var model = new DietListViewModel
        {
            UserId = userId,
            LatestDiet = dietTask.Result,
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

    private string ResolveUserId(string? userId)
    {
        if (User.IsInRole("User"))
            return User.Identity?.Name ?? "default_user";

        return string.IsNullOrEmpty(userId) ? User.Identity?.Name ?? "default_user" : userId;
    }
}
