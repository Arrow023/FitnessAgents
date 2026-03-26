using FitnessAgentsWeb.Core.Configuration;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
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
                new OpenAIClientOptions
                {
                    Endpoint = new Uri(aiEndpoint),
                    NetworkTimeout = TimeSpan.FromMinutes(5)
                }
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
                string json = aiResponse.Substring(start, end - start + 1).Trim();
                return SanitizeJson(json);
            }
            return "{}";
        }

        /// <summary>
        /// Fixes common AI JSON mistakes: unquoted property keys like { exercise": ... }
        /// </summary>
        private static string SanitizeJson(string json)
        {
            // Fix unquoted keys: matches patterns like { key" or , key" and adds the missing opening quote
            return Regex.Replace(json, @"([{,]\s*)([a-zA-Z_][a-zA-Z0-9_]*)("")\s*:", "$1\"$2\":");
        }

        protected DateTime GetAppNow()
        {
            string tzId = _configProvider.GetAppTimezone();
            return TimezoneHelper.GetAppNow(tzId);
        }
    }
}
