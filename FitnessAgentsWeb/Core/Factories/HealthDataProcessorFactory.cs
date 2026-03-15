using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Core.Services;
using Microsoft.Extensions.Configuration;

namespace FitnessAgentsWeb.Core.Factories
{
    public class HealthDataProcessorFactory
    {
        private readonly IConfiguration _configuration;
        private readonly IStorageRepository _storageRepository;

        public HealthDataProcessorFactory(IConfiguration configuration, StorageRepositoryFactory storageFactory)
        {
            _configuration = configuration;
            _storageRepository = storageFactory.Create();
        }

        public IHealthDataProcessor Create()
        {
            return new HealthConnectDataProcessor(_storageRepository, _configuration);
        }
    }
}
