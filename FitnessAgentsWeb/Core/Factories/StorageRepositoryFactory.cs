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

        public StorageRepositoryFactory(IConfiguration configuration, IWebHostEnvironment env)
        {
            _configuration = configuration;
            _env = env;
        }

        public IStorageRepository Create()
        {
            // Defaulting to Firebase as per single-tenant architecture
            return new FirebaseStorageRepository(_configuration);
        }
    }
}
