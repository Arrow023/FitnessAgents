using FitnessAgentsWeb.Core.Configuration;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Core.Services;
using Microsoft.Extensions.Configuration;

namespace FitnessAgentsWeb.Core.Factories
{
    public class NotificationServiceFactory
    {
        private readonly IConfiguration _configuration;
        private readonly IAppConfigurationProvider _configProvider;

        public NotificationServiceFactory(IConfiguration configuration, ConfigurationProviderFactory providerFactory)
        {
            _configuration = configuration;
            _configProvider = providerFactory.GetProvider();
        }

        public INotificationService Create()
        {
            string notifType = _configuration["FactorySettings:NotificationType"] ?? "SMTP";

            if (notifType == "SMTP")
            {
                return new SmtpEmailNotificationService(_configProvider);
            }

            // Fallback
            return new SmtpEmailNotificationService(_configProvider);
        }
    }
}
