using FitnessAgentsWeb.Core.Configuration;
using FitnessAgentsWeb.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.ClientModel;
using System.Linq;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Embeddings;

namespace FitnessAgentsWeb.Core.Services
{
    /// <summary>
    /// Generates text embeddings using the same AI provider configured for the app
    /// (NVIDIA NIM / OpenAI-compatible endpoint). Uses the embedding model from config.
    /// </summary>
    public class EmbeddingService : IEmbeddingService
    {
        private readonly IAppConfigurationProvider _configProvider;
        private readonly ILogger<EmbeddingService> _logger;

        public EmbeddingService(IAppConfigurationProvider configProvider, ILogger<EmbeddingService> logger)
        {
            _configProvider = configProvider;
            _logger = logger;
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text)
        {
            try
            {
                // Use dedicated embedding config; fall back to main AI config if not set
                string apiKey = _configProvider.GetEmbeddingApiKey();
                if (string.IsNullOrEmpty(apiKey))
                    apiKey = _configProvider.GetAiKey();

                string endpoint = _configProvider.GetEmbeddingEndpoint();
                if (string.IsNullOrEmpty(endpoint))
                    endpoint = _configProvider.GetAiEndpoint();

                if (!string.IsNullOrEmpty(endpoint) && endpoint.EndsWith("/chat/completions"))
                    endpoint = endpoint.Replace("/chat/completions", "");

                var client = new OpenAIClient(
                    new ApiKeyCredential(apiKey),
                    new OpenAIClientOptions
                    {
                        Endpoint = new Uri(endpoint),
                        NetworkTimeout = TimeSpan.FromMinutes(5)
                    }
                );

                string embeddingModel = _configProvider.GetEmbeddingModel();
                if (string.IsNullOrEmpty(embeddingModel))
                    embeddingModel = "text-embedding-3-small";

                var embeddingClient = client.GetEmbeddingClient(embeddingModel);

                var result = await embeddingClient.GenerateEmbeddingAsync(text);
                var embedding = result.Value.ToFloats();

                _logger.LogInformation("[EmbeddingService] Generated {Dims}-dim embedding", embedding.Length);
                return embedding.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EmbeddingService] Failed to generate embedding, returning empty vector");
                return [];
            }
        }
    }
}
