using FitnessAgentsWeb.Models;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Core.Interfaces
{
    public interface IAiAgentService
    {
        Task<string> GenerateWorkoutAsync(UserHealthContext context);
    }
}
