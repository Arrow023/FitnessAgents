using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly IStorageRepository _storageRepository;
        private readonly Core.Configuration.IAppConfigurationManager _appConfig;
        private readonly Microsoft.Extensions.Logging.ILogger<AdminController> _logger;

        public AdminController(IStorageRepository storageRepository, Core.Configuration.IAppConfigurationManager appConfig, Microsoft.Extensions.Logging.ILogger<AdminController> logger)
        {
            _storageRepository = storageRepository;
            _appConfig = appConfig;
            _logger = logger;
        }

        public async Task<IActionResult> Settings()
        {
            return View(_appConfig);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateSettings(
            string adminEmail, string adminPassword, 
            string aiModel, string aiEndpoint, string aiKey, 
            string ocrModel, string ocrEndpoint, string ocrKey,
            string smtpHost, string smtpPort, string fromEmail, string smtpPassword,
            string timezone)
        {
            await _appConfig.SaveSetupSettingsAsync(
                adminEmail, adminPassword, 
                aiModel, aiEndpoint, aiKey, 
                ocrModel, ocrEndpoint, ocrKey,
                smtpHost, smtpPort, fromEmail, smtpPassword,
                timezone);

            return RedirectToAction("Settings");
        }

        public async Task<IActionResult> Users()
        {
            var users = await _storageRepository.GetAllUserProfilesAsync();
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            ViewBag.BaseUrl = baseUrl;
            return View(users);
        }

        // Lists available log files (from Logs folder)
        public IActionResult Logs()
        {
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
            var logsDir = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            var filePath = Path.Combine(logsDir, name);
            if (!System.IO.File.Exists(filePath)) return NotFound();

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
    }
}
