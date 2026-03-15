using FitnessAgentsWeb.Models;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Core.Interfaces
{
    public interface IHealthDataProcessor
    {
        Task<HealthExportPayload> ProcessAndMergeHealthDataAsync(string userId, HealthExportPayload newPayload);
        Task<UserHealthContext> LoadHealthStateToRAMAsync(string userId, HealthExportPayload payload);
    }
}
