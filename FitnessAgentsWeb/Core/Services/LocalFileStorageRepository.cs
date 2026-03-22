using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Models;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Core.Services
{
    public class LocalFileStorageRepository : IStorageRepository
    {
        private readonly string _appDataFolder;
        private readonly string _healthFilePath;
        private readonly string _historyFilePath;
        private TimeZoneInfo _istZone;

        public LocalFileStorageRepository(IWebHostEnvironment env)
        {
            _appDataFolder = Path.Combine(env.ContentRootPath, "App_Data");
            Directory.CreateDirectory(_appDataFolder);

            _healthFilePath = Path.Combine(_appDataFolder, "health_connect_today.json");
            _historyFilePath = Path.Combine(_appDataFolder, "weekly_workout_history.json");

            try { _istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); }
            catch
            {
                try { _istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
                catch { _istZone = TimeZoneInfo.Local; }
            }
        }

        public async Task<HealthExportPayload?> GetTodayHealthDataAsync(string userId)
        {
            string path = Path.Combine(_appDataFolder, $"health_connect_today_{userId}.json");
            if (!File.Exists(path))
                return null;

            string json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<HealthExportPayload>(json);
        }

        public async Task SaveTodayHealthDataAsync(string userId, string jsonPayload)
        {
            string path = Path.Combine(_appDataFolder, $"health_connect_today_{userId}.json");
            await File.WriteAllTextAsync(path, jsonPayload);
        }

        public async Task<WeeklyWorkoutHistory?> GetWeeklyHistoryAsync(string userId)
        {
            DateTime nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _istZone);
            int diff = (7 + (nowIst.DayOfWeek - DayOfWeek.Sunday)) % 7;
            DateTime currentWeekSunday = nowIst.AddDays(-1 * diff).Date;

            WeeklyWorkoutHistory history = new() { WeekStartDate = currentWeekSunday };
            string path = Path.Combine(_appDataFolder, $"weekly_workout_history_{userId}.json");

            if (File.Exists(path))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(path);
                    var existingHistory = JsonSerializer.Deserialize<WeeklyWorkoutHistory>(json);
                    
                    if (existingHistory != null && existingHistory.WeekStartDate == currentWeekSunday)
                    {
                        history = existingHistory;
                    }
                }
                catch { }
            }

            return history;
        }

        public async Task SaveWeeklyHistoryAsync(string userId, WeeklyWorkoutHistory history)
        {
            string path = Path.Combine(_appDataFolder, $"weekly_workout_history_{userId}.json");
            string json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path, json);
        }

        public async Task<WeeklyDietHistory?> GetWeeklyDietHistoryAsync(string userId)
        {
            DateTime nowIst = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _istZone);
            int diff = (7 + (nowIst.DayOfWeek - DayOfWeek.Sunday)) % 7;
            DateTime currentWeekSunday = nowIst.AddDays(-1 * diff).Date;

            WeeklyDietHistory history = new() { WeekStartDate = currentWeekSunday };
            string path = Path.Combine(_appDataFolder, $"weekly_diet_history_{userId}.json");

            if (File.Exists(path))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(path);
                    var existingHistory = JsonSerializer.Deserialize<WeeklyDietHistory>(json);
                    
                    if (existingHistory != null && existingHistory.WeekStartDate == currentWeekSunday)
                    {
                        history = existingHistory;
                    }
                }
                catch { }
            }

            return history;
        }

        public async Task SaveWeeklyDietHistoryAsync(string userId, WeeklyDietHistory history)
        {
            string path = Path.Combine(_appDataFolder, $"weekly_diet_history_{userId}.json");
            string json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path, json);
        }

        public async Task<InBodyExport?> GetLatestInBodyDataAsync(string userId)
        {
            string path = Path.Combine(_appDataFolder, $"latest_inbody_{userId}.json");
            if (!File.Exists(path))
                return null;
            string json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<InBodyExport>(json);
        }

        public async Task SaveLatestInBodyDataAsync(string userId, string jsonPayload)
        {
            string path = Path.Combine(_appDataFolder, $"latest_inbody_{userId}.json");
            await File.WriteAllTextAsync(path, jsonPayload);
        }

        public Task<System.Collections.Generic.Dictionary<string, UserProfile>> GetAllUserProfilesAsync()
        {
            // Throwing NotImplemented or returning empty because local storage is deprecated
            return Task.FromResult(new System.Collections.Generic.Dictionary<string, UserProfile>());
        }

        public Task<UserProfile?> GetUserProfileAsync(string userId)
        {
            return Task.FromResult<UserProfile?>(null);
        }

        public Task SaveUserProfileAsync(string userId, UserProfile profile)
        {
            return Task.CompletedTask;
        }

        public Task DeleteUserAsync(string userId)
        {
            return Task.CompletedTask;
        }

        public Task<DietPlan?> GetLatestDietAsync(string userId)
        {
            return Task.FromResult<DietPlan?>(null);
        }

        public Task SaveLatestDietAsync(string userId, DietPlan diet)
        {
            return Task.CompletedTask;
        }

        public Task<PlanFeedback?> GetPlanFeedbackAsync(string userId, string planId)
        {
            return Task.FromResult<PlanFeedback?>(null);
        }

        public Task SavePlanFeedbackAsync(string userId, PlanFeedback feedback)
        {
            return Task.CompletedTask;
        }

        public Task<List<PlanFeedback>> GetRecentFeedbackAsync(string userId, int count = 5)
        {
            return Task.FromResult(new List<PlanFeedback>());
        }

        public Task<DailyDiary?> GetDiaryEntryAsync(string userId, string date)
        {
            return Task.FromResult<DailyDiary?>(null);
        }

        public Task SaveDiaryEntryAsync(string userId, DailyDiary entry)
        {
            return Task.CompletedTask;
        }

        public Task<List<DailyDiary>> GetRecentDiaryEntriesAsync(string userId, int days = 7)
        {
            return Task.FromResult(new List<DailyDiary>());
        }

        public Task<WeeklyDigest?> GetWeeklyDigestAsync(string userId, string weekStart)
        {
            return Task.FromResult<WeeklyDigest?>(null);
        }

        public Task SaveWeeklyDigestAsync(string userId, WeeklyDigest digest)
        {
            return Task.CompletedTask;
        }
    }
}
