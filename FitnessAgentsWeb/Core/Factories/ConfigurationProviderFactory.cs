using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace FitnessAgentsWeb.Core.Factories
{
    public class ConfigurationProviderFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;

        public ConfigurationProviderFactory(IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
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
            Console.WriteLine("[ConfigurationProviderFactory] Local config empty. Falling back to Firebase.");
            return new Configuration.FirebaseSettingsProvider(_configuration);
        }
    }
}
