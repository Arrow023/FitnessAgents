using FitnessAgentsWeb.Models;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Core.Interfaces
{
    public interface IAiOrchestratorService
    {
        /// <summary>
        /// Appends new health data from the webhook without triggering plan generation.
        /// </summary>
        Task<bool> AppendHealthDataAsync(string userId, HealthExportPayload newPayload);

        /// <summary>
        /// Full plan generation pipeline. Pass an optional <paramref name="progress"/> reporter
        /// to receive real-time status updates for background job tracking.
        /// </summary>
        Task<bool> ProcessAndGenerateAsync(
            string userId,
            HealthExportPayload? newPayload = null,
            bool sendEmail = true,
            IProgress<(PlanGenerationStatus Status, string Step)>? progress = null);

        /// <summary>
        /// Re-sends a previously stored diet plan via email.
        /// </summary>
        Task<bool> EmailStoreDietPlanAsync(string userId, DietPlan diet);

        /// <summary>
        /// Manually triggers weekly diary digest generation for a user.
        /// </summary>
        Task<bool> TriggerWeeklyDigestAsync(string userId);
    }
}
