using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Controllers
{
    [ApiController]
    public class WebhooksController : ControllerBase
    {
        private readonly IAiOrchestratorService _orchestrator;
        private readonly IStorageRepository _storageRepository;

        public WebhooksController(IAiOrchestratorService orchestrator, IStorageRepository storageRepository)
        {
            _orchestrator = orchestrator;
            _storageRepository = storageRepository;
        }

        [HttpPost("/api/webhooks/{userId}/generate-workout")]
        public async Task<IActionResult> GenerateWorkout(string userId)
        {
            userId = userId?.ToLowerInvariant() ?? "default_user";

            // Security Check
            var profiles = await _storageRepository.GetAllUserProfilesAsync();
            if (profiles.TryGetValue(userId, out var profile))
            {
                if (!string.IsNullOrWhiteSpace(profile.WebhookHeaderKey) && !string.IsNullOrWhiteSpace(profile.WebhookHeaderValue))
                {
                    if (!Request.Headers.TryGetValue(profile.WebhookHeaderKey, out var headerValue) || headerValue != profile.WebhookHeaderValue)
                    {
                        return Unauthorized("Invalid webhook security header.");
                    }
                }
            }
            else
            {
                return Unauthorized("User profile not found.");
            }

            using var reader = new StreamReader(Request.Body);
            var incomingJson = await reader.ReadToEndAsync();
            
            HealthExportPayload? payload = null;
            if (!string.IsNullOrWhiteSpace(incomingJson))
            {
                try
                {
                    payload = JsonSerializer.Deserialize<HealthExportPayload>(incomingJson);
                }
                catch
                {
                    // Ignore empty or invalid JSON, allow AI to generate based on existing data
                }
            }

            if (payload != null)
            {
                await _orchestrator.AppendHealthDataAsync(userId, payload);
            }

            return Ok("Data received and saved successfully.");
        }
    }
}
