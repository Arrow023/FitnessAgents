using FitnessAgentsWeb.Core.Configuration;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Core.Services;
using FitnessAgentsWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessAgentsWeb.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly IStorageRepository _storageRepository;
    private readonly InBodyOcrService _ocrService;

    public ProfileController(IStorageRepository storageRepository, InBodyOcrService ocrService)
    {
        _storageRepository = storageRepository;
        _ocrService = ocrService;
    }

    public async Task<IActionResult> Index(string? userId = null)
    {
        userId = ResolveUserId(userId);
        ViewData["ActiveNav"] = "profile";

        var profile = await _storageRepository.GetUserProfileAsync(userId);
        var baseUrl = $"{Request.Scheme}://{Request.Host}";

        var model = new ProfileViewModel
        {
            UserId = userId,
            Profile = profile,
            WebhookUrl = $"{baseUrl}/api/webhooks/{userId}/generate-workout"
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> UpdatePreferences(
        string userId, string email, string notificationTime, string preferences, string foodPreferences,
        string firstName, string lastName, string newPassword, string? webhookHeaderKey, string? webhookHeaderValue,
        string scheduleMonday, string scheduleTuesday, string scheduleWednesday, string scheduleThursday,
        string scheduleFriday, string scheduleSaturday, string scheduleSunday)
    {
        userId = ResolveUserId(userId);

        var profiles = await _storageRepository.GetAllUserProfilesAsync();
        if (profiles.TryGetValue(userId, out var profile))
        {
            profile.Email = email;
            profile.NotificationTime = notificationTime;
            profile.Preferences = preferences;
            profile.FoodPreferences = foodPreferences;
            profile.FirstName = firstName;
            profile.LastName = lastName;
            profile.WebhookHeaderKey = webhookHeaderKey;
            profile.WebhookHeaderValue = webhookHeaderValue;

            profile.WorkoutSchedule["Monday"] = scheduleMonday;
            profile.WorkoutSchedule["Tuesday"] = scheduleTuesday;
            profile.WorkoutSchedule["Wednesday"] = scheduleWednesday;
            profile.WorkoutSchedule["Thursday"] = scheduleThursday;
            profile.WorkoutSchedule["Friday"] = scheduleFriday;
            profile.WorkoutSchedule["Saturday"] = scheduleSaturday;
            profile.WorkoutSchedule["Sunday"] = scheduleSunday;

            if (!string.IsNullOrEmpty(newPassword))
            {
                profile.PasswordHash = PasswordHasher.HashPassword(newPassword);
            }

            await _storageRepository.SaveUserProfileAsync(userId, profile);
        }

        return RedirectToAction("Index", new { userId });
    }

    [HttpPost]
    public async Task<IActionResult> UploadInBody(string userId, IFormFile inBodyImage)
    {
        userId = ResolveUserId(userId);

        if (inBodyImage is not null && inBodyImage.Length > 0)
        {
            using var stream = inBodyImage.OpenReadStream();
            string mimeType = inBodyImage.ContentType ?? "image/jpeg";
            string extractedJson = await _ocrService.ExtractInBodyJsonAsync(stream, mimeType);
            await _storageRepository.SaveLatestInBodyDataAsync(userId, extractedJson);
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
