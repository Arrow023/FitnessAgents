using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Core.Services;
using Microsoft.Extensions.Configuration;

namespace FitnessAgentsWeb.Core.Factories
{
    public class HealthDataProcessorFactory
    {
        private readonly IConfiguration _configuration;
        private readonly IStorageRepository _storageRepository;
        private readonly Microsoft.Extensions.Logging.ILoggerFactory _loggerFactory;

        public HealthDataProcessorFactory(IConfiguration configuration, StorageRepositoryFactory storageFactory, Microsoft.Extensions.Logging.ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            _storageRepository = storageFactory.Create();
            _loggerFactory = loggerFactory;
        }

        public IHealthDataProcessor Create()
        {
            var logger = _loggerFactory.CreateLogger<HealthConnectDataProcessor>();
            return new HealthConnectDataProcessor(_storageRepository, _configuration, logger);
        }
    }
}
