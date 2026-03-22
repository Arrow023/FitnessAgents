using FitnessAgentsWeb.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Core.Interfaces
{
    public interface IStorageRepository
    {
        Task<HealthExportPayload?> GetTodayHealthDataAsync(string userId);
        Task SaveTodayHealthDataAsync(string userId, string jsonPayload);
        
        Task<WeeklyWorkoutHistory?> GetWeeklyHistoryAsync(string userId);
        Task SaveWeeklyHistoryAsync(string userId, WeeklyWorkoutHistory history);

        Task<WeeklyDietHistory?> GetWeeklyDietHistoryAsync(string userId);
        Task SaveWeeklyDietHistoryAsync(string userId, WeeklyDietHistory history);

        Task<InBodyExport?> GetLatestInBodyDataAsync(string userId);
        Task SaveLatestInBodyDataAsync(string userId, string jsonPayload);

        Task<System.Collections.Generic.Dictionary<string, UserProfile>> GetAllUserProfilesAsync();
        Task<UserProfile?> GetUserProfileAsync(string userId);
        Task SaveUserProfileAsync(string userId, UserProfile profile);
        Task DeleteUserAsync(string userId);

        Task<DietPlan?> GetLatestDietAsync(string userId);
        Task SaveLatestDietAsync(string userId, DietPlan diet);

        // Plan feedback
        Task<PlanFeedback?> GetPlanFeedbackAsync(string userId, string planId);
        Task SavePlanFeedbackAsync(string userId, PlanFeedback feedback);
        Task<List<PlanFeedback>> GetRecentFeedbackAsync(string userId, int count = 5);

        // Daily diary
        Task<DailyDiary?> GetDiaryEntryAsync(string userId, string date);
        Task SaveDiaryEntryAsync(string userId, DailyDiary entry);
        Task<List<DailyDiary>> GetRecentDiaryEntriesAsync(string userId, int days = 7);

        // Weekly digests
        Task<WeeklyDigest?> GetWeeklyDigestAsync(string userId, string weekStart);
        Task SaveWeeklyDigestAsync(string userId, WeeklyDigest digest);
    }
}
