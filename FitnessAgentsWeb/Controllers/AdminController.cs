using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly IStorageRepository _storageRepository;
        private readonly IAiOrchestratorService _orchestratorService;
        private readonly Core.Configuration.IAppConfigurationManager _appConfig;
        private readonly Microsoft.Extensions.Logging.ILogger<AdminController> _logger;
        private readonly IPlanGenerationTracker _tracker;
        private readonly Channel<PlanGenerationJob> _jobChannel;

        public AdminController(
            IStorageRepository storageRepository,
            IAiOrchestratorService orchestratorService,
            Core.Configuration.IAppConfigurationManager appConfig,
            Microsoft.Extensions.Logging.ILogger<AdminController> logger,
            IPlanGenerationTracker tracker,
            Channel<PlanGenerationJob> jobChannel)
        {
            _storageRepository = storageRepository;
            _orchestratorService = orchestratorService;
            _appConfig = appConfig;
            _logger = logger;
            _tracker = tracker;
            _jobChannel = jobChannel;
        }

        public async Task<IActionResult> Settings()
        {
            ViewData["ActiveNav"] = "settings";
            return View(_appConfig);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateSettings(
            string adminEmail, string adminPassword, 
            string aiModel, string aiEndpoint, string aiKey, 
            string ocrModel, string ocrEndpoint, string ocrKey,
            string smtpHost, string smtpPort, string fromEmail, string smtpPassword,
            string timezone,
            string qdrantEndpoint, string qdrantApiKey,
            string embeddingModel, string embeddingEndpoint, string embeddingApiKey, string embeddingDimension)
        {
            await _appConfig.SaveSetupSettingsAsync(
                adminEmail, adminPassword, 
                aiModel, aiEndpoint, aiKey, 
                ocrModel, ocrEndpoint, ocrKey,
                smtpHost, smtpPort, fromEmail, smtpPassword,
                timezone, "",
                qdrantEndpoint, qdrantApiKey,
                embeddingModel, embeddingEndpoint, embeddingApiKey, embeddingDimension);

            return RedirectToAction("Settings");
        }

        public async Task<IActionResult> Users()
        {
            ViewData["ActiveNav"] = "users";
            var users = await _storageRepository.GetAllUserProfilesAsync();
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            ViewBag.BaseUrl = baseUrl;
            return View(users);
        }

        // Lists available log files (from Logs folder)
        public IActionResult Logs()
        {
            ViewData["ActiveNav"] = "logs";
            var logsDir = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            if (!Directory.Exists(logsDir)) Directory.CreateDirectory(logsDir);

            var files = Directory.GetFiles(logsDir, "fitness-assist-*.log")
                .Select(f => new { Name = Path.GetFileName(f), Path = f })
                .OrderByDescending(f => f.Name)
                .ToList();

            return View(files);
        }

        // Returns raw content of a specific log file
        public IActionResult LogFile(string name)
        {
            if (string.IsNullOrEmpty(name)) return BadRequest();
            // Prevent path traversal
            var safeName = Path.GetFileName(name);
            var logsDir = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            var filePath = Path.Combine(logsDir, safeName);
            var fullPath = Path.GetFullPath(filePath);
            if (!fullPath.StartsWith(Path.GetFullPath(logsDir))) return BadRequest();
            if (!System.IO.File.Exists(fullPath)) return NotFound();

            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                var content = reader.ReadToEnd();
                return Content(content, "text/plain");
            }
        }

        [HttpPost]
        public async Task<IActionResult> AddUser(string userId, string password, string firstName, string lastName)
        {
            userId = userId?.Trim().ToLowerInvariant() ?? "";
            var profile = new UserProfile
            {
                FirstName = firstName,
                LastName = lastName,
                PasswordHash = Core.Configuration.PasswordHasher.HashPassword(password),
                IsActive = true
            };

            await _storageRepository.SaveUserProfileAsync(userId, profile);
            return RedirectToAction("Users");
        }

        [HttpPost]
        public async Task<IActionResult> ToggleUser(string userId)
        {
            var users = await _storageRepository.GetAllUserProfilesAsync();
            if (users.TryGetValue(userId, out var profile))
            {
                profile.IsActive = !profile.IsActive;
                await _storageRepository.SaveUserProfileAsync(userId, profile);
            }
            return RedirectToAction("Users");
        }

        [HttpPost]
        public async Task<IActionResult> ToggleAdmin(string userId)
        {
            var users = await _storageRepository.GetAllUserProfilesAsync();
            if (users.TryGetValue(userId, out var profile))
            {
                profile.IsAdmin = !profile.IsAdmin;
                await _storageRepository.SaveUserProfileAsync(userId, profile);
            }
            return RedirectToAction("Users");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest();

            _logger.LogWarning("[Admin] Deleting user {UserId}", userId);
            await _storageRepository.DeleteUserAsync(userId);
            return RedirectToAction("Users");
        }

        [HttpPost]
        public async Task<IActionResult> GeneratePlans(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return BadRequest();

            var jobId = _tracker.Enqueue(userId);
            if (jobId is null)
            {
                TempData["JobResult"] = $"A plan is already being generated for {userId}. Please wait.";
                TempData["JobSuccess"] = "False";
                return RedirectToAction("Users");
            }

            var job = new PlanGenerationJob { JobId = jobId, UserId = userId };
            await _jobChannel.Writer.WriteAsync(job);

            TempData["JobResult"] = $"Plan generation queued for {userId} (Job: {jobId}).";
            TempData["JobSuccess"] = "True";
            return RedirectToAction("Users");
        }

        public async Task<IActionResult> Jobs()
        {
            ViewData["ActiveNav"] = "jobs";
            var users = await _storageRepository.GetAllUserProfilesAsync();
            return View(users);
        }

        [HttpPost]
        public async Task<IActionResult> TriggerWorkoutScheduler(string userId)
        {
            _logger.LogInformation("[Admin] Manual workout scheduler trigger for {UserId}", userId);

            var success = await _orchestratorService.ProcessAndGenerateAsync(userId);

            TempData["JobResult"] = success
                ? $"Workout scheduler triggered successfully for {userId}."
                : $"Workout scheduler failed for {userId}. Check logs for details.";
            TempData["JobSuccess"] = success.ToString();

            return RedirectToAction("Jobs");
        }

        [HttpPost]
        public async Task<IActionResult> TriggerWeeklyDigest(string userId)
        {
            _logger.LogInformation("[Admin] Manual weekly digest trigger for {UserId}", userId);

            var success = await _orchestratorService.TriggerWeeklyDigestAsync(userId);

            TempData["JobResult"] = success
                ? $"Weekly digest generated successfully for {userId}."
                : $"Weekly digest generation failed for {userId}. Check logs for details.";
            TempData["JobSuccess"] = success.ToString();

            return RedirectToAction("Jobs");
        }

        [HttpPost]
        public async Task<IActionResult> TriggerWorkoutSchedulerAll()
        {
            _logger.LogInformation("[Admin] Manual workout scheduler trigger for ALL users");
            var users = await _storageRepository.GetAllUserProfilesAsync();
            var results = new List<string>();

            foreach (var kv in users.Where(u => u.Value.IsActive))
            {
                var success = await _orchestratorService.ProcessAndGenerateAsync(kv.Key);
                results.Add($"{kv.Key}: {(success ? "OK" : "FAILED")}");
            }

            TempData["JobResult"] = $"Workout scheduler completed for all users. {string.Join(", ", results)}";
            TempData["JobSuccess"] = results.All(r => r.Contains("OK")).ToString();

            return RedirectToAction("Jobs");
        }

        [HttpPost]
        public async Task<IActionResult> TriggerWeeklyDigestAll()
        {
            _logger.LogInformation("[Admin] Manual weekly digest trigger for ALL users");
            var users = await _storageRepository.GetAllUserProfilesAsync();
            var results = new List<string>();

            foreach (var kv in users.Where(u => u.Value.IsActive))
            {
                var success = await _orchestratorService.TriggerWeeklyDigestAsync(kv.Key);
                results.Add($"{kv.Key}: {(success ? "OK" : "FAILED")}");
            }

            TempData["JobResult"] = $"Weekly digest completed for all users. {string.Join(", ", results)}";
            TempData["JobSuccess"] = results.All(r => r.Contains("OK")).ToString();

            return RedirectToAction("Jobs");
        }
    }
}
