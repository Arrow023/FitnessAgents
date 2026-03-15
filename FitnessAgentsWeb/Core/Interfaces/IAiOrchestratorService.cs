using FitnessAgentsWeb.Models;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Core.Interfaces
{
    public interface IAiOrchestratorService
    {
        // For the webhook to append data only
        Task<bool> AppendHealthDataAsync(string userId, HealthExportPayload newPayload);

        // For the background scheduler or manual trigger (can skip email)
        Task<bool> ProcessAndGenerateAsync(string userId, HealthExportPayload? newPayload = null, bool sendEmail = true);

        // Explicitly send stored plans
        Task<bool> EmailStoreDietPlanAsync(string userId, DietPlan diet);
    }
}
