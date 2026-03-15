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
        private readonly Microsoft.Extensions.Logging.ILoggerFactory _loggerFactory;

        public NotificationServiceFactory(IConfiguration configuration, ConfigurationProviderFactory providerFactory, Microsoft.Extensions.Logging.ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            _configProvider = providerFactory.GetProvider();
            _loggerFactory = loggerFactory;
        }

        public INotificationService Create()
        {
            string notifType = _configuration["FactorySettings:NotificationType"] ?? "SMTP";
            var logger = _loggerFactory.CreateLogger<SmtpEmailNotificationService>();

            if (notifType == "SMTP")
            {
                return new SmtpEmailNotificationService(_configProvider, logger);
            }

            // Fallback
            return new SmtpEmailNotificationService(_configProvider, logger);
        }
    }
}
