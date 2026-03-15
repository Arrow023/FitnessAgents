using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace FitnessAgentsWeb.Core.Factories
{
    public class ConfigurationProviderFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly Microsoft.Extensions.Logging.ILogger<ConfigurationProviderFactory> _logger;

        public ConfigurationProviderFactory(IServiceProvider serviceProvider, IConfiguration configuration, Microsoft.Extensions.Logging.ILogger<ConfigurationProviderFactory> logger)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = logger;
        }

        public Configuration.IAppConfigurationProvider GetProvider()
        {
            // First check if Local is configured
            var localProvider = new Configuration.LocalSettingsProvider(_configuration);
            if (localProvider.IsConfigured())
            {
                return localProvider;
            }

            // Fallback to Firebase
            _logger.LogInformation("[ConfigurationProviderFactory] Local config empty. Falling back to Firebase.");
            return new Configuration.FirebaseSettingsProvider(_configuration, _serviceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Configuration.FirebaseSettingsProvider>>());
        }
    }
}
