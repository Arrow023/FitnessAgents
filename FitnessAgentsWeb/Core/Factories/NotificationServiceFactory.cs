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
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;

        public NotificationServiceFactory(IConfiguration configuration, ConfigurationProviderFactory providerFactory, Microsoft.Extensions.Logging.ILoggerFactory loggerFactory, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            _configuration = configuration;
            _configProvider = providerFactory.GetProvider();
            _loggerFactory = loggerFactory;
            _env = env;
        }

        public INotificationService Create()
        {
            string notifType = _configuration["FactorySettings:NotificationType"] ?? "SMTP";
            var logger = _loggerFactory.CreateLogger<SmtpEmailNotificationService>();

            if (notifType == "SMTP")
            {
                return new SmtpEmailNotificationService(_configProvider, logger, _env);
            }

            // Fallback
            return new SmtpEmailNotificationService(_configProvider, logger, _env);
        }
    }
}
