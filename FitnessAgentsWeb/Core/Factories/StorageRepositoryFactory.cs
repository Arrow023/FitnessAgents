using FitnessAgentsWeb.Core.Interfaces;
using FitnessAgentsWeb.Core.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace FitnessAgentsWeb.Core.Factories
{
    public class StorageRepositoryFactory
    {
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;
        private readonly Microsoft.Extensions.Logging.ILoggerFactory _loggerFactory;

        public StorageRepositoryFactory(IConfiguration configuration, IWebHostEnvironment env, Microsoft.Extensions.Logging.ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            _env = env;
            _loggerFactory = loggerFactory;
        }

        public IStorageRepository Create()
        {
            // Defaulting to Firebase as per single-tenant architecture
            var logger = _loggerFactory.CreateLogger<FirebaseStorageRepository>();
            return new FirebaseStorageRepository(_configuration, logger);
        }
    }
}
