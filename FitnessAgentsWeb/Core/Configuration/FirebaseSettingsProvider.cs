using Firebase.Database;
using Firebase.Database.Query;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Core.Configuration
{
    public class FirebaseSettingsProvider : IAppConfigurationManager
    {
        private readonly FirebaseClient _realtimeDb;
        private string _userId;
        
        // Caching for sync interface
        private string _aiModel = string.Empty;
        private string _aiKey = string.Empty;
        private string _aiEndpoint = string.Empty;
        
        // OCR AI 
        private string _ocrModel = string.Empty;
        private string _ocrKey = string.Empty;
        private string _ocrEndpoint = string.Empty;

        private string _smtpPassword = string.Empty;
        private string _fromEmail = string.Empty;
        private string _toEmail = string.Empty;

        public FirebaseSettingsProvider(IConfiguration configuration)
        {
            string databaseUrl = Environment.GetEnvironmentVariable("FIREBASE_DATABASE_URL") 
                                 ?? configuration["FirebaseSettings:DatabaseUrl"] 
                                 ?? "https://fitnessagent-1ef17-default-rtdb.asia-southeast1.firebasedatabase.app/";
            
            _realtimeDb = new FirebaseClient(databaseUrl);
            
            // For now, hardcode or pass the user ID. In a real app with auth, this comes from the token.
            _userId = configuration["FirebaseSettings:DefaultUserId"] ?? "default_user";

            // We must load asynchronously during app startup or on first request, 
            // but since the interface is sync, we'll block here or expect an init call.
            LoadSettingsAsync().GetAwaiter().GetResult();
        }

        public async Task<bool> IsAppConfiguredAsync()
        {
            try
            {
                var snapshot = await _realtimeDb
                    .Child("config")
                    .Child("app_settings")
                    .OnceSingleAsync<Dictionary<string, object>>();
                    
                return snapshot != null && snapshot.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task SaveSetupSettingsAsync(
            string adminEmail, string adminPassword, 
            string aiModel, string aiEndpoint, string aiKey, 
            string ocrModel, string ocrEndpoint, string ocrKey,
            string smtpHost, string smtpPort, string fromEmail, string smtpPassword)
        {
            var configData = new Dictionary<string, object>
            {
                { "AdminEmail", adminEmail },
                { "AdminPassword", adminPassword }, // In production, hash this!
                { "AiModel", aiModel },
                { "AiEndpoint", aiEndpoint },
                { "AiKey", aiKey },
                { "OcrModel", ocrModel },
                { "OcrEndpoint", ocrEndpoint },
                { "OcrKey", ocrKey },
                { "SmtpHost", smtpHost },
                { "SmtpPort", smtpPort },
                { "FromEmail", fromEmail },
                { "SmtpPassword", smtpPassword }
            };

            // Save global configuration
            await _realtimeDb
                .Child("config")
                .Child("app_settings")
                .PutAsync(configData);
            
            // Reload into memory
            await LoadSettingsAsync();
        }

        public async Task<bool> ValidateAdminLoginAsync(string email, string password)
        {
            try
            {
                var snapshot = await _realtimeDb
                    .Child("config")
                    .Child("app_settings")
                    .OnceSingleAsync<Dictionary<string, object>>();
                    
                if (snapshot != null)
                {
                    string dbEmail = snapshot.ContainsKey("AdminEmail") ? snapshot["AdminEmail"].ToString()! : "";
                    string dbPass = snapshot.ContainsKey("AdminPassword") ? snapshot["AdminPassword"].ToString()! : "";
                    
                    return string.Equals(dbEmail, email, StringComparison.OrdinalIgnoreCase) && dbPass == password;
                }
            }
            catch { }
            return false;
        }

        private async Task LoadSettingsAsync()
        {
            if (_realtimeDb == null) return;
            
            try
            {
                var snapshot = await _realtimeDb
                    .Child("config")
                    .Child("app_settings")
                    .OnceSingleAsync<Dictionary<string, object>>();

                if (snapshot != null)
                {
                    _aiModel = snapshot.ContainsKey("AiModel") ? snapshot["AiModel"].ToString()! : "";
                    _aiKey = snapshot.ContainsKey("AiKey") ? snapshot["AiKey"].ToString()! : "";
                    _aiEndpoint = snapshot.ContainsKey("AiEndpoint") ? snapshot["AiEndpoint"].ToString()! : "";

                    _ocrModel = snapshot.ContainsKey("OcrModel") ? snapshot["OcrModel"].ToString()! : "";
                    _ocrKey = snapshot.ContainsKey("OcrKey") ? snapshot["OcrKey"].ToString()! : "";
                    _ocrEndpoint = snapshot.ContainsKey("OcrEndpoint") ? snapshot["OcrEndpoint"].ToString()! : "";

                    _smtpPassword = snapshot.ContainsKey("SmtpPassword") ? snapshot["SmtpPassword"].ToString()! : "";
                    _fromEmail = snapshot.ContainsKey("FromEmail") ? snapshot["FromEmail"].ToString()! : "";
                    _toEmail = snapshot.ContainsKey("ToEmail") ? snapshot["ToEmail"].ToString()! : "";
                    
                    Console.WriteLine($"[FirebaseSettingsProvider] Loaded global settings from Realtime DB");
                }
                else
                {
                    Console.WriteLine($"[FirebaseSettingsProvider] No global settings found in Realtime DB");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FirebaseSettingsProvider] Error loading settings: {ex.Message}");
            }
        }

        public string GetAiModel() => _aiModel;
        public string GetAiKey() => _aiKey;
        public string GetAiEndpoint() => _aiEndpoint;
        
        public string GetOcrModel() => _ocrModel;
        public string GetOcrKey() => _ocrKey;
        public string GetOcrEndpoint() => _ocrEndpoint;

        public string GetSmtpPassword() => _smtpPassword;
        public string GetFromEmail() => _fromEmail;
        public string GetToEmail() => _toEmail; // Can be linked to admin email for now
    }
}
