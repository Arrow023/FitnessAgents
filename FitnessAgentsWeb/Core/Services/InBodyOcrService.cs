using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;

namespace FitnessAgentsWeb.Core.Services
{
    public class InBodyOcrService
    {
        private readonly Configuration.IAppConfigurationManager _configManager;

        public InBodyOcrService(Configuration.IAppConfigurationManager configManager)
        {
            _configManager = configManager;
        }

        public async Task<string> ExtractInBodyJsonAsync(Stream imageStream, string mimeType)
        {
            string aiKey = _configManager.GetOcrKey();
            string aiEndpoint = _configManager.GetOcrEndpoint();
            string ocrModel = _configManager.GetOcrModel();

            // Fallback to main AI config if OCR is completely blank
            if (string.IsNullOrEmpty(aiKey)) aiKey = _configManager.GetAiKey();
            if (string.IsNullOrEmpty(aiEndpoint)) aiEndpoint = _configManager.GetAiEndpoint();
            if (string.IsNullOrEmpty(ocrModel)) ocrModel = "meta/llama-3.2-90b-vision-instruct";

            using MemoryStream ms = new MemoryStream();
            await imageStream.CopyToAsync(ms);
            string base64Image = Convert.ToBase64String(ms.ToArray());

            var prompt = @"You are a clinical data extraction engine. I am providing you with a scan image of an InBody body composition analysis.
                Extract the numbers exactly as they appear and map them strictly to the following JSON structure. 
                OUTPUT ONLY THE RAW JSON FORMATTED OBJECT. Do not include any conversational text, greetings, markdown blocks (like ```json), or explanatory words before or after the JSON. The very first character of your response MUST be '{' and the last MUST be '}'.

                {
                    ""scan_date"": ""YYYY-MM-DD"",
                    ""demographics"": {
                        ""gender"": ""Male | Female"",
                        ""age"": 0,
                        ""height_cm"": 0.0
                    },
                    ""core_composition"": {
                        ""weight_kg"": 0.0,
                        ""skeletal_muscle_mass_kg"": 0.0,
                        ""body_fat_mass_kg"": 0.0,
                        ""percent_body_fat"": 0.0,
                        ""bmi"": 0.0
                    },
                    ""metabolic_health"": {
                        ""basal_metabolic_rate_kcal"": 0,
                        ""visceral_fat_level"": 0,
                        ""waist_hip_ratio"": 0.0
                    },
                    ""segmental_lean_analysis"": {
                        ""right_arm_evaluation"": ""Normal | Under | Over"",
                        ""left_arm_evaluation"": ""Normal | Under | Over"",
                        ""trunk_evaluation"": ""Normal | Under | Over"",
                        ""right_leg_evaluation"": ""Normal | Under | Over"",
                        ""left_leg_evaluation"": ""Normal | Under | Over""
                    },
                    ""inbody_targets"": {
                        ""target_weight_kg"": 0.0,
                        ""fat_control_kg"": 0.0,
                        ""muscle_control_kg"": 0.0,
                        ""inbody_score"": 0
                    }
                }";

            var payload = new
            {
                model = ocrModel,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = prompt },
                            new { type = "image_url", image_url = new { url = $"data:{mimeType};base64,{base64Image}" } }
                        }
                    }
                },
                max_tokens = 512,
                temperature = 0.0
            };

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aiKey);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            
            // Construct full URL if endpoint doesn't end with chat/completions
            string fullUrl = aiEndpoint;
            if (!fullUrl.EndsWith("/chat/completions"))
            {
                fullUrl = fullUrl.TrimEnd('/') + "/chat/completions";
            }
            
            var response = await client.PostAsync(fullUrl, content);

            string responseJson = await response.Content.ReadAsStringAsync();

            try
            {
                using JsonDocument doc = JsonDocument.Parse(responseJson);
                string result = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";
                
                // Clean markdown boundaries
                result = result.Trim();
                if (result.StartsWith("```json")) result = result.Substring(7);
                else if (result.StartsWith("```")) result = result.Substring(3);
                if (result.EndsWith("```")) result = result.Substring(0, result.Length - 3);

                // Aggressively find only the JSON block to discard any hallucinated conversational text
                int firstBrace = result.IndexOf('{');
                int lastBrace = result.LastIndexOf('}');
                if (firstBrace >= 0 && lastBrace >= firstBrace)
                {
                    result = result.Substring(firstBrace, lastBrace - firstBrace + 1);
                }

                return result.Trim();
            }
            catch
            {
                return "{}";
            }
        }
    }
}
