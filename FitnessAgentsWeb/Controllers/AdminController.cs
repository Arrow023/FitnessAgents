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

        public AdminController(IStorageRepository storageRepository)
        {
            _storageRepository = storageRepository;
        }

        public async Task<IActionResult> Users()
        {
            var users = await _storageRepository.GetAllUserProfilesAsync();
            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            ViewBag.BaseUrl = baseUrl;
            return View(users);
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
