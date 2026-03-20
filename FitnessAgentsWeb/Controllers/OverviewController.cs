using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitnessAgentsWeb.Controllers;

[Authorize]
public class OverviewController : Controller
{
    private readonly IStorageRepository _storageRepository;
    private readonly IHealthDataProcessor _healthDataProcessor;

    public OverviewController(IStorageRepository storageRepository, IHealthDataProcessor healthDataProcessor)
    {
        _storageRepository = storageRepository;
        _healthDataProcessor = healthDataProcessor;
    }

    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> Index(string? userId = null)
    {
        userId = ResolveUserId(userId);
        ViewData["ActiveNav"] = "overview";

        var healthDataTask = _storageRepository.GetTodayHealthDataAsync(userId);
        var inBodyTask = _storageRepository.GetLatestInBodyDataAsync(userId);
        await Task.WhenAll(healthDataTask, inBodyTask);

        var healthData = healthDataTask.Result;
        var userContext = await _healthDataProcessor.LoadHealthStateToRAMAsync(userId, healthData);

        var model = new OverviewViewModel
        {
            UserId = userId,
            Context = userContext,
            InBody = inBodyTask.Result,
            HealthData = healthData
        };

        return View(model);
    }

    private string ResolveUserId(string? userId)
    {
        if (User.IsInRole("User"))
            return User.Identity?.Name ?? "default_user";

        return string.IsNullOrEmpty(userId) ? User.Identity?.Name ?? "default_user" : userId;
    }
}
