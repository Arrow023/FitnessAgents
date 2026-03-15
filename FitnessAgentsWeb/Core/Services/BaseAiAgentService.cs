using FitnessAgentsWeb.Core.Configuration;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using FitnessAgentsWeb.Core.Helpers;

namespace FitnessAgentsWeb.Core.Services
{
    public abstract class BaseAiAgentService
    {
        protected readonly IAppConfigurationProvider _configProvider;
        protected readonly ILogger _logger;

        protected BaseAiAgentService(IAppConfigurationProvider configProvider, ILogger logger)
        {
            _configProvider = configProvider;
            _logger = logger;
        }

        protected IChatClient GetChatClient()
        {
            string aiKey = _configProvider.GetAiKey();
            string aiEndpoint = _configProvider.GetAiEndpoint();
            string aiModel = _configProvider.GetAiModel();

            // OpenAIClient expects the base URL, but aiEndpoint might have /chat/completions
            if (!string.IsNullOrEmpty(aiEndpoint) && aiEndpoint.EndsWith("/chat/completions"))
            {
                aiEndpoint = aiEndpoint.Replace("/chat/completions", "");
            }

            var openAiClient = new OpenAIClient(
                new ApiKeyCredential(aiKey),
                new OpenAIClientOptions { Endpoint = new Uri(aiEndpoint) }
            );

            return openAiClient.GetChatClient(aiModel).AsIChatClient();
        }

        protected string ExtractJson(string aiResponse)
        {
            if (string.IsNullOrEmpty(aiResponse)) return "{}";

            int start = aiResponse.IndexOf('{');
            int end = aiResponse.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                return aiResponse.Substring(start, end - start + 1).Trim();
            }
            return "{}";
        }

        protected DateTime GetAppNow()
        {
            string tzId = _configProvider.GetAppTimezone();
            return TimezoneHelper.GetAppNow(tzId);
        }
    }
}
