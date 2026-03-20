using System.Threading.Tasks;

namespace FitnessAgentsWeb.Core.Configuration
{
    public interface IAppConfigurationManager : IAppConfigurationProvider
    {
        Task SaveSetupSettingsAsync(
            string adminEmail, string adminPassword, 
            string aiModel, string aiEndpoint, string aiKey, 
            string ocrModel, string ocrEndpoint, string ocrKey,
            string smtpHost, string smtpPort, string fromEmail, string smtpPassword,
            string timezone, string firebaseDatabaseSecret = "");

        Task<bool> IsAppConfiguredAsync();
        
        Task<bool> ValidateAdminLoginAsync(string email, string password);

        string GetOcrModel();
        string GetOcrKey();
        string GetOcrEndpoint();
    }
}
