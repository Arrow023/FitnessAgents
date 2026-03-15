using FitnessAgentsWeb.Models;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Core.Interfaces
{
    public interface IAiOrchestratorService
    {
        // For the webhook to append data only
        Task<bool> AppendHealthDataAsync(string userId, HealthExportPayload newPayload);

        // For the background scheduler to run the AI
        Task<bool> ProcessAndGenerateAsync(string userId, HealthExportPayload? newPayload = null);
    }
}
