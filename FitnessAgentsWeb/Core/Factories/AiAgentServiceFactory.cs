using FitnessAgentsWeb.Core.Configuration;
using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Core.Services;
using Microsoft.Extensions.Configuration;

namespace FitnessAgentsWeb.Core.Factories
{
    public class AiAgentServiceFactory
    {
        private readonly IConfiguration _configuration;
        private readonly IAppConfigurationProvider _configProvider;

        public AiAgentServiceFactory(IConfiguration configuration, ConfigurationProviderFactory providerFactory)
        {
            _configuration = configuration;
            _configProvider = providerFactory.GetProvider();
        }

        public IAiAgentService Create()
        {
            string aiType = _configuration["FactorySettings:AiType"] ?? "NVIDIA";

            if (aiType == "NVIDIA")
            {
                return new NvidiaNimAgentService(_configProvider);
            }

            // Fallback
            return new NvidiaNimAgentService(_configProvider);
        }
    }
}
