using FitnessAgentsWeb.Models;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Core.Interfaces
{
    public interface IStorageRepository
    {
        Task<HealthExportPayload?> GetTodayHealthDataAsync(string userId);
        Task SaveTodayHealthDataAsync(string userId, string jsonPayload);
        
        Task<WeeklyWorkoutHistory?> GetWeeklyHistoryAsync(string userId);
        Task SaveWeeklyHistoryAsync(string userId, WeeklyWorkoutHistory history);

        Task<InBodyExport?> GetLatestInBodyDataAsync(string userId);
        Task SaveLatestInBodyDataAsync(string userId, string jsonPayload);

        Task<System.Collections.Generic.Dictionary<string, UserProfile>> GetAllUserProfilesAsync();
        Task SaveUserProfileAsync(string userId, UserProfile profile);

        Task<DietPlan?> GetLatestDietAsync(string userId);
        Task SaveLatestDietAsync(string userId, DietPlan diet);
    }
}
