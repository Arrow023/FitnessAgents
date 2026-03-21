using Firebase.Database;
using Firebase.Database.Query;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Core.Services
{
    public class FirebaseStorageRepository : IStorageRepository
    {
        private readonly FirebaseClient _firebaseClient;
        private readonly Microsoft.Extensions.Logging.ILogger<FirebaseStorageRepository> _logger;

        public FirebaseStorageRepository(IConfiguration configuration, Microsoft.Extensions.Logging.ILogger<FirebaseStorageRepository> logger)
        {
            _logger = logger;
            // Prefer environment variable (set through docker or admin UI)
            string databaseUrl = Environment.GetEnvironmentVariable("FIREBASE_DATABASE_URL") 
                                 ?? configuration["FirebaseSettings:DatabaseUrl"] 
                                 ?? "https://fitnessagent-1ef17-default-rtdb.asia-southeast1.firebasedatabase.app/";
            
            string databaseSecret = Environment.GetEnvironmentVariable("FIREBASE_DATABASE_SECRET") 
                                     ?? configuration["FirebaseSettings:DatabaseSecret"] 
                                     ?? "";

            _firebaseClient = new FirebaseClient(databaseUrl, new Firebase.Database.FirebaseOptions
            {
                AuthTokenAsyncFactory = () => Task.FromResult(databaseSecret)
            });
        }

        private string Norm(string userId) => userId?.Trim().ToLowerInvariant() ?? "default_user";

        // --- HEALTH DATA ---
        public async Task<HealthExportPayload?> GetTodayHealthDataAsync(string userId)
        {
            userId = Norm(userId);
            try
            {
                var snapshot = await _firebaseClient
                    .Child("users")
                    .Child(userId)
                    .Child("health_connect")
                    .OnceSingleAsync<HealthExportPayload>();
                
                return snapshot;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[FirebaseStorage] Failed getting health data for {userId}");
                return null;
            }
        }

        public async Task SaveTodayHealthDataAsync(string userId, string jsonPayload)
        {
            userId = Norm(userId);
            try
            {
                var payload = JsonSerializer.Deserialize<HealthExportPayload>(jsonPayload);
                await _firebaseClient
                    .Child("users")
                    .Child(userId)
                    .Child("health_connect")
                    .PutAsync(payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[FirebaseStorage] Failed saving health data for {userId}");
            }
        }

        // --- WEEKLY HISTORY ---
        public async Task<WeeklyWorkoutHistory?> GetWeeklyHistoryAsync(string userId)
        {
            userId = Norm(userId);
            // ... rest of method ...
            // Determine timezone for the current week slice
            TimeZoneInfo istZone;
            try { istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
            catch { 
                try { istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
                catch { istZone = TimeZoneInfo.Local; } 
            }

            DateTime nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            int diff = (7 + (nowIst.DayOfWeek - DayOfWeek.Sunday)) % 7;
            DateTime currentWeekSunday = nowIst.AddDays(-1 * diff).Date;
            
            WeeklyWorkoutHistory history = new() { WeekStartDate = currentWeekSunday };

            try
            {
                var snapshot = await _firebaseClient
                    .Child("users")
                    .Child(userId)
                    .Child("weekly_workout_history")
                    .OnceSingleAsync<WeeklyWorkoutHistory>();
                
                if (snapshot != null && snapshot.WeekStartDate == currentWeekSunday)
                {
                    history = snapshot;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"[FirebaseStorage] History unavailable for {userId}. Creating new week slice.");
            }

            return history;
        }

        public async Task SaveWeeklyHistoryAsync(string userId, WeeklyWorkoutHistory history)
        {
            userId = Norm(userId);
            try
            {
                await _firebaseClient
                    .Child("users")
                    .Child(userId)
                    .Child("weekly_workout_history")
                    .PutAsync(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[FirebaseStorage] Failed saving weekly history for {userId}");
            }
        }

        public async Task<WeeklyDietHistory?> GetWeeklyDietHistoryAsync(string userId)
        {
            userId = Norm(userId);
            TimeZoneInfo istZone;
            try { istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
            catch { 
                try { istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
                catch { istZone = TimeZoneInfo.Local; } 
            }

            DateTime nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, istZone);
            int diff = (7 + (nowIst.DayOfWeek - DayOfWeek.Sunday)) % 7;
            DateTime currentWeekSunday = nowIst.AddDays(-1 * diff).Date;
            
            WeeklyDietHistory history = new() { WeekStartDate = currentWeekSunday };

            try
            {
                var snapshot = await _firebaseClient
                    .Child("users")
                    .Child(userId)
                    .Child("weekly_diet_history")
                    .OnceSingleAsync<WeeklyDietHistory>();
                
                if (snapshot != null && snapshot.WeekStartDate == currentWeekSunday)
                {
                    history = snapshot;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"[FirebaseStorage] Diet history unavailable for {userId}. Creating new week slice.");
            }

            return history;
        }

        public async Task SaveWeeklyDietHistoryAsync(string userId, WeeklyDietHistory history)
        {
            userId = Norm(userId);
            try
            {
                await _firebaseClient
                    .Child("users")
                    .Child(userId)
                    .Child("weekly_diet_history")
                    .PutAsync(history);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[FirebaseStorage] Failed saving weekly diet history for {userId}");
            }
        }

        // --- INBODY ---
        public async Task<InBodyExport?> GetLatestInBodyDataAsync(string userId)
        {
            userId = Norm(userId);
            try
            {
                var snapshot = await _firebaseClient
                    .Child("users")
                    .Child(userId)
                    .Child("inbody")
                    .OnceSingleAsync<InBodyExport>();
                
                return snapshot;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[FirebaseStorage] Failed getting InBody data for {userId}");
                return null;
            }
        }

        public async Task SaveLatestInBodyDataAsync(string userId, string jsonPayload)
        {
            userId = Norm(userId);
            try
            {
                var payload = JsonSerializer.Deserialize<InBodyExport>(jsonPayload);
                await _firebaseClient
                    .Child("users")
                    .Child(userId)
                    .Child("inbody")
                    .PutAsync(payload);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[FirebaseStorage] Failed saving InBody data for {userId}");
            }
        }

        // --- USER PROFILES ---
        public async Task<System.Collections.Generic.Dictionary<string, UserProfile>> GetAllUserProfilesAsync()
        {
            var users = new System.Collections.Generic.Dictionary<string, UserProfile>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var snapshot = await _firebaseClient
                    .Child("users")
                    .OnceAsync<dynamic>();

                foreach (var userNode in snapshot)
                {
                    string uid = userNode.Key;
                    try
                    {
                        var profileSnapshot = await _firebaseClient
                            .Child("users")
                            .Child(uid)
                            .Child("profile")
                            .OnceSingleAsync<UserProfile>();
                            
                        if (profileSnapshot != null)
                        {
                            users[uid] = profileSnapshot;
                        }
                    }
                    catch { /* Profile missing for this user, skip */ }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FirebaseStorage] Failed fetching user profiles");
            }
            return users;
        }

        public async Task<UserProfile?> GetUserProfileAsync(string userId)
        {
            userId = Norm(userId);
            try
            {
                var snapshot = await _firebaseClient
                    .Child("users")
                    .Child(userId)
                    .Child("profile")
                    .OnceSingleAsync<UserProfile>();
                
                return snapshot;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[FirebaseStorage] Failed getting profile for {userId}");
                return null;
            }
        }

        public async Task SaveUserProfileAsync(string userId, UserProfile profile)
        {
            userId = Norm(userId);
            try
            {
                await _firebaseClient
                    .Child("users")
                    .Child(userId)
                    .Child("profile")
                    .PutAsync(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[FirebaseStorage] Failed saving profile for {userId}");
            }
        }

        // --- DIET PLAN ---
        public async Task<DietPlan?> GetLatestDietAsync(string userId)
        {
            userId = Norm(userId);
            try
            {
                var snapshot = await _firebaseClient
                    .Child("users")
                    .Child(userId)
                    .Child("diet")
                    .OnceSingleAsync<DietPlan>();
                
                return snapshot;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[FirebaseStorage] Failed getting diet plan for {userId}");
                return null;
            }
        }

        public async Task SaveLatestDietAsync(string userId, DietPlan diet)
        {
            userId = Norm(userId);
            try
            {
                await _firebaseClient
                    .Child("users")
                    .Child(userId)
                    .Child("diet")
                    .PutAsync(diet);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[FirebaseStorage] Failed saving diet plan for {userId}");
            }
        }

        // --- PLAN FEEDBACK ---
        public async Task<PlanFeedback?> GetPlanFeedbackAsync(string userId, string planId)
        {
            userId = Norm(userId);
            try
            {
                var snapshot = await _firebaseClient
                    .Child("users")
                    .Child(userId)
                    .Child("feedback")
                    .Child(planId)
                    .OnceSingleAsync<PlanFeedback>();

                return snapshot;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FirebaseStorage] Failed getting feedback for {UserId}/{PlanId}", userId, planId);
                return null;
            }
        }

        public async Task SavePlanFeedbackAsync(string userId, PlanFeedback feedback)
        {
            userId = Norm(userId);
            try
            {
                await _firebaseClient
                    .Child("users")
                    .Child(userId)
                    .Child("feedback")
                    .Child(feedback.PlanId)
                    .PutAsync(feedback);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FirebaseStorage] Failed saving feedback for {UserId}/{PlanId}", userId, feedback.PlanId);
            }
        }

        public async Task<List<PlanFeedback>> GetRecentFeedbackAsync(string userId, int count = 5)
        {
            userId = Norm(userId);
            try
            {
                var snapshot = await _firebaseClient
                    .Child("users")
                    .Child(userId)
                    .Child("feedback")
                    .OrderByKey()
                    .LimitToLast(count)
                    .OnceAsync<PlanFeedback>();

                return snapshot
                    .Select(f => f.Object)
                    .OrderByDescending(f => f.FeedbackDate)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FirebaseStorage] Failed getting recent feedback for {UserId}", userId);
                return [];
            }
        }

        public async Task<DailyDiary?> GetDiaryEntryAsync(string userId, string date)
        {
            userId = Norm(userId);
            try
            {
                return await _firebaseClient
                    .Child("users").Child(userId).Child("diary").Child(date)
                    .OnceSingleAsync<DailyDiary>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FirebaseStorage] Failed getting diary for {UserId} on {Date}", userId, date);
                return null;
            }
        }

        public async Task SaveDiaryEntryAsync(string userId, DailyDiary entry)
        {
            userId = Norm(userId);
            try
            {
                entry.UpdatedAt = DateTime.UtcNow;
                await _firebaseClient
                    .Child("users").Child(userId).Child("diary").Child(entry.Date)
                    .PutAsync(entry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FirebaseStorage] Failed saving diary for {UserId} on {Date}", userId, entry.Date);
            }
        }

        public async Task<List<DailyDiary>> GetRecentDiaryEntriesAsync(string userId, int days = 7)
        {
            userId = Norm(userId);
            try
            {
                var snapshot = await _firebaseClient
                    .Child("users").Child(userId).Child("diary")
                    .OrderByKey()
                    .LimitToLast(days)
                    .OnceAsync<DailyDiary>();

                return snapshot
                    .Select(d => d.Object)
                    .OrderByDescending(d => d.Date)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FirebaseStorage] Failed getting recent diary for {UserId}", userId);
                return [];
            }
        }

        public async Task<WeeklyDigest?> GetWeeklyDigestAsync(string userId, string weekStart)
        {
            userId = Norm(userId);
            try
            {
                return await _firebaseClient
                    .Child("users").Child(userId).Child("weekly_digests").Child(weekStart)
                    .OnceSingleAsync<WeeklyDigest>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FirebaseStorage] Failed getting weekly digest for {UserId} week {Week}", userId, weekStart);
                return null;
            }
        }

        public async Task SaveWeeklyDigestAsync(string userId, WeeklyDigest digest)
        {
            userId = Norm(userId);
            try
            {
                await _firebaseClient
                    .Child("users").Child(userId).Child("weekly_digests").Child(digest.WeekStart)
                    .PutAsync(digest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[FirebaseStorage] Failed saving weekly digest for {UserId} week {Week}", userId, digest.WeekStart);
            }
        }
    }
}
