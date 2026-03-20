using FitnessAgentsWeb.Core.Helpers;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessAgentsWeb.Controllers;

[Authorize]
public class WorkoutController : Controller
{
    private readonly IStorageRepository _storageRepository;
    private readonly IAiOrchestratorService _orchestrator;
    private readonly INotificationService _notificationService;
    private readonly IHealthDataProcessor _healthDataProcessor;

    public WorkoutController(
        IStorageRepository storageRepository,
        IAiOrchestratorService orchestrator,
        INotificationService notificationService,
        IHealthDataProcessor healthDataProcessor)
    {
        _storageRepository = storageRepository;
        _orchestrator = orchestrator;
        _notificationService = notificationService;
        _healthDataProcessor = healthDataProcessor;
    }

    public async Task<IActionResult> Index(string? userId = null)
    {
        userId = ResolveUserId(userId);
        ViewData["ActiveNav"] = "workout";

        var history = await _storageRepository.GetWeeklyHistoryAsync(userId);

        var renderedHtml = new Dictionary<string, string>();
        if (history is not null)
        {
            foreach (var kvp in history.PastWorkouts)
            {
                renderedHtml[kvp.Key] = MarkdownStylingHelper.RenderToEmailHtml(kvp.Value);
            }
        }

        var model = new WorkoutListViewModel
        {
            UserId = userId,
            WeeklyHistory = history,
            RenderedHtml = renderedHtml
        };

        return View(model);
    }

    public async Task<IActionResult> Detail(string userId, string day)
    {
        userId = ResolveUserId(userId);
        ViewData["ActiveNav"] = "workout";

        var history = await _storageRepository.GetWeeklyHistoryAsync(userId);
        string html = "";

        if (history is not null && history.PastWorkouts.TryGetValue(day, out var markdown))
        {
            html = MarkdownStylingHelper.RenderToEmailHtml(markdown);
        }

        var model = new WorkoutDetailViewModel
        {
            UserId = userId,
            DayOfWeek = day,
            RenderedHtml = html
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Generate(string userId)
    {
        userId = ResolveUserId(userId);
        await _orchestrator.ProcessAndGenerateAsync(userId, null, sendEmail: false);
        return RedirectToAction("Index", new { userId });
    }

    [HttpPost]
    public async Task<IActionResult> ResendEmail(string userId, string dayOfWeek)
    {
        userId = ResolveUserId(userId);
        var profile = await _storageRepository.GetUserProfileAsync(userId);
        var history = await _storageRepository.GetWeeklyHistoryAsync(userId);

        if (profile is not null && history is not null && history.PastWorkouts.TryGetValue(dayOfWeek, out var planMarkdown))
        {
            var userContext = await _healthDataProcessor.LoadHealthStateToRAMAsync(userId, null);
            await _notificationService.SendWorkoutNotificationAsync(profile.Email, planMarkdown, userContext);
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
