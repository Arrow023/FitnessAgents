using FitnessAgentsWeb.Core.Helpers;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models;
using FitnessAgentsWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Threading.Channels;

namespace FitnessAgentsWeb.Controllers;

[Authorize]
public class WorkoutController : Controller
{
    private readonly IStorageRepository _storageRepository;
    private readonly IAiOrchestratorService _orchestrator;
    private readonly INotificationService _notificationService;
    private readonly IHealthDataProcessor _healthDataProcessor;
    private readonly IPlanGenerationTracker _tracker;
    private readonly Channel<PlanGenerationJob> _jobChannel;

    public WorkoutController(
        IStorageRepository storageRepository,
        IAiOrchestratorService orchestrator,
        INotificationService notificationService,
        IHealthDataProcessor healthDataProcessor,
        IPlanGenerationTracker tracker,
        Channel<PlanGenerationJob> jobChannel)
    {
        _storageRepository = storageRepository;
        _orchestrator = orchestrator;
        _notificationService = notificationService;
        _healthDataProcessor = healthDataProcessor;
        _tracker = tracker;
        _jobChannel = jobChannel;
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

    /// <summary>
    /// Enqueues an async plan generation job and returns the job ID immediately.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Generate(string userId)
    {
        userId = ResolveUserId(userId);

        var jobId = _tracker.Enqueue(userId);
        if (jobId is null)
        {
            return Json(new { error = "A plan is already being generated. Please wait." });
        }

        var job = new PlanGenerationJob { JobId = jobId, UserId = userId };
        await _jobChannel.Writer.WriteAsync(job);

        return Json(new { jobId });
    }

    /// <summary>
    /// Server-Sent Events endpoint that streams real-time job progress to the browser.
    /// </summary>
    [HttpGet]
    public async Task GenerateStatus(string jobId, CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

        while (!ct.IsCancellationRequested)
        {
            var job = _tracker.GetJob(jobId);
            if (job is null)
            {
                await Response.WriteAsync($"data: {{\"status\":\"Failed\",\"currentStep\":\"Job not found\"}}\n\n", ct);
                await Response.Body.FlushAsync(ct);
                break;
            }

            var payload = JsonSerializer.Serialize(new
            {
                job.Status,
                job.CurrentStep,
                job.ErrorMessage
            }, jsonOptions);

            await Response.WriteAsync($"data: {payload}\n\n", ct);
            await Response.Body.FlushAsync(ct);

            if (job.Status is PlanGenerationStatus.Completed or PlanGenerationStatus.Failed)
            {
                break;
            }

            await Task.Delay(800, ct);
        }
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

    [HttpGet]
    public async Task<IActionResult> Feedback(string userId, string day)
    {
        userId = ResolveUserId(userId);
        ViewData["ActiveNav"] = "workout";

        string planId = $"{userId}_{day}_workout";
        var existing = await _storageRepository.GetPlanFeedbackAsync(userId, planId);

        var model = new PlanFeedbackViewModel
        {
            UserId = userId,
            DayOfWeek = day,
            PlanType = "workout",
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
            PlanId = $"{model.UserId}_{model.DayOfWeek}_workout",
            PlanType = "workout",
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

    private string ResolveUserId(string? userId)
    {
        if (User.IsInRole("User"))
            return User.Identity?.Name ?? "default_user";

        return string.IsNullOrEmpty(userId) ? User.Identity?.Name ?? "default_user" : userId;
    }
}
