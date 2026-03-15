using Microsoft.Extensions.Configuration;

namespace FitnessAgentsWeb.Core.Configuration
{
    public class LocalSettingsProvider : IAppConfigurationProvider
    {
        private readonly IConfiguration _configuration;

        public LocalSettingsProvider(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GetAiModel() => _configuration["AiSettings:Model"] ?? string.Empty;
        public string GetAiKey() => _configuration["AiSettings:ApiKey"] ?? string.Empty;
        public string GetAiEndpoint() => _configuration["AiSettings:Endpoint"] ?? string.Empty;
        public string GetSmtpPassword() => _configuration["SMTP:AppPassword"] ?? string.Empty;
        public string GetFromEmail() => _configuration["SMTP:FromEmail"] ?? string.Empty;
        public string GetToEmail() => _configuration["SMTP:ToEmail"] ?? string.Empty;
        
        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(GetAiKey()) && !string.IsNullOrEmpty(GetSmtpPassword());
        }
    }
}
