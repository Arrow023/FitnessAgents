using FitnessAgentsWeb.Core.Configuration;
using FitnessAgentsWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Controllers
{
    public class AuthController : Controller
    {
        private readonly IAppConfigurationManager _configManager;

        public AuthController(IAppConfigurationManager configManager)
        {
            _configManager = configManager;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Login()
        {
            if (!await _configManager.IsAppConfiguredAsync())
            {
                return RedirectToAction("Index", "Setup");
            }

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Overview");
            }
            return View(new LoginViewModel());
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(string email, string password, [FromServices] Core.Interfaces.IStorageRepository storageRepo)
        {
            // Note: 'email' field in form is now used as 'username' or 'userId'
            string username = email?.Trim().ToLowerInvariant() ?? "";
            
            // 1. Check if this is the Admin Login
            bool isAdmin = await _configManager.ValidateAdminLoginAsync(username, password);
            if (isAdmin)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, username),
                    new Claim(ClaimTypes.Role, "Admin")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));
                return RedirectToAction("Index", "Overview");
            }

            // 2. Check if this is a Standard User Login
            var users = await storageRepo.GetAllUserProfilesAsync();
            if (users.TryGetValue(username, out var profile))
            {
                if (profile.IsActive && PasswordHasher.VerifyPassword(password, profile.PasswordHash))
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, username),
                        new Claim(ClaimTypes.Role, "User")
                    };

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));
                    return RedirectToAction("Index", "Overview", new { userId = username });
                }
            }

            return View(new LoginViewModel { ErrorMessage = "Invalid credentials or account disabled." });
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }
    }
}
