using FitnessAgentsWeb.Core.Factories;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IStorageRepository _storageRepository;
        private readonly Core.Services.InBodyOcrService _ocrService;
        private readonly IAiOrchestratorService _orchestrator;

        public DashboardController(IStorageRepository storageRepository, Core.Services.InBodyOcrService ocrService, IAiOrchestratorService orchestrator)
        {
            _storageRepository = storageRepository;
            _ocrService = ocrService;
            _orchestrator = orchestrator;
        }

        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> Index(string? userId = null)
        {
            if (User.IsInRole("User"))
            {
                // Force strictly to the logged-in user so caching/URLs cannot spoof
                userId = User.Identity?.Name ?? "default_user";
            }
            else if (string.IsNullOrEmpty(userId))
            {
                userId = "default_user";
            }

            ViewBag.UserId = userId;
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            ViewBag.WebhookUrl = $"{baseUrl}/api/webhooks/{userId}/generate-workout";

            // Attempt to load current data for this user ID
            var healthData = await _storageRepository.GetTodayHealthDataAsync(userId);
            var history = await _storageRepository.GetWeeklyHistoryAsync(userId);
            var inBody = await _storageRepository.GetLatestInBodyDataAsync(userId);
            var diet = await _storageRepository.GetLatestDietAsync(userId);
            
            var profiles = await _storageRepository.GetAllUserProfilesAsync();
            if (profiles.TryGetValue(userId, out var profile))
            {
                ViewBag.UserProfile = profile;
            }

            ViewBag.HasHealthData = healthData != null;
            ViewBag.HasHistory = history != null && history.PastWorkouts.Count > 0;
            ViewBag.HasInBody = inBody != null;
            
            // Pass data down for Chart.js and Tabs
            ViewBag.HealthData = healthData;
            ViewBag.InBodyData = inBody;
            ViewBag.HistoryData = history;
            ViewBag.DietData = diet;

            // NEW: Use the unified processor to get the structured context (IST filtered, Name/InBody mapped)
            var processor = HttpContext.RequestServices.GetRequiredService<IHealthDataProcessor>();
            var userContext = await processor.LoadHealthStateToRAMAsync(userId, healthData);
            ViewBag.UserHealthContext = userContext;

            // Pre-render history to HTML for the modal
            var historyHtml = new System.Collections.Generic.Dictionary<string, string>();
            if (history != null)
            {
                foreach (var kvp in history.PastWorkouts)
                {
                    historyHtml[kvp.Key] = Core.Helpers.MarkdownStylingHelper.RenderToEmailHtml(kvp.Value);
                }
            }
            ViewBag.HistoryHtml = historyHtml;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ResendPlan(string userId, string dayOfWeek, [FromServices] INotificationService notificationService, [FromServices] HealthDataProcessorFactory processorFactory)
        {
            var profile = (await _storageRepository.GetAllUserProfilesAsync()).GetValueOrDefault(userId);
            var history = await _storageRepository.GetWeeklyHistoryAsync(userId);
            
            if (profile != null && history != null && history.PastWorkouts.ContainsKey(dayOfWeek))
            {
                string planMarkdown = history.PastWorkouts[dayOfWeek];
                var healthProcessor = processorFactory.Create();
                var userContext = await healthProcessor.LoadHealthStateToRAMAsync(userId, null);
                
                await notificationService.SendWorkoutNotificationAsync(profile.Email, planMarkdown, userContext);
            }

            return RedirectToAction("Index", new { userId = userId });
        }

        [HttpPost]
        public async Task<IActionResult> UploadInBody(string userId, Microsoft.AspNetCore.Http.IFormFile inBodyImage)
        {
            if (inBodyImage != null && inBodyImage.Length > 0)
            {
                using var stream = inBodyImage.OpenReadStream();
                string mimeType = inBodyImage.ContentType ?? "image/jpeg";
                
                // Execute OCR Vision Agent
                string extractedJson = await _ocrService.ExtractInBodyJsonAsync(stream, mimeType);
                
                // Save JSON direct to Firebase for this user
                await _storageRepository.SaveLatestInBodyDataAsync(userId, extractedJson);
            }

            return RedirectToAction("Index", new { userId = userId });
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePreferences(string userId, string email, string notificationTime, string preferences, string firstName, string lastName, string newPassword)
        {
            var profiles = await _storageRepository.GetAllUserProfilesAsync();
            if (profiles.TryGetValue(userId, out var profile))
            {
                profile.Email = email;
                profile.NotificationTime = notificationTime;
                profile.Preferences = preferences;
                profile.FirstName = firstName;
                profile.LastName = lastName;
                
                if (!string.IsNullOrEmpty(newPassword))
                {
                    profile.PasswordHash = Core.Configuration.PasswordHasher.HashPassword(newPassword);
                }

                await _storageRepository.SaveUserProfileAsync(userId, profile);
            }
            return RedirectToAction("Index", new { userId = userId });
        }

        [HttpPost]
        public IActionResult TriggerPlanEmail(string userId)
        {
            userId = userId?.ToLowerInvariant();
            // Fire and Forget exactly as requested
            _ = Task.Run(() => _orchestrator.ProcessAndGenerateAsync(userId));
            return RedirectToAction("Index", new { userId = userId });
        }
    }
}
