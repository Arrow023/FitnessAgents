using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FitnessAgentsWeb.Core.Configuration;

namespace FitnessAgentsWeb.Core.Services;

/// <summary>
/// Uses the Vision LLM (same endpoint as InBody OCR) to extract meal information from food photos.
/// Returns structured JSON with food items, estimated portions, and meal type.
/// </summary>
public class MealVisionService
{
    private readonly IAppConfigurationManager _configManager;

    public MealVisionService(IAppConfigurationManager configManager)
    {
        _configManager = configManager;
    }

    /// <summary>
    /// Extracts meal data from a food photo using the Vision LLM.
    /// Returns a JSON string with detected food items, quantities, and meal type.
    /// </summary>
    public async Task<MealVisionResult> ExtractMealFromImageAsync(Stream imageStream, string mimeType)
    {
        string aiKey = _configManager.GetOcrKey();
        string aiEndpoint = _configManager.GetOcrEndpoint();
        string ocrModel = _configManager.GetOcrModel();

        // Fallback to main AI config if OCR is completely blank
        if (string.IsNullOrEmpty(aiKey)) aiKey = _configManager.GetAiKey();
        if (string.IsNullOrEmpty(aiEndpoint)) aiEndpoint = _configManager.GetAiEndpoint();
        if (string.IsNullOrEmpty(ocrModel)) ocrModel = "meta/llama-3.2-90b-vision-instruct";

        using var ms = new MemoryStream();
        await imageStream.CopyToAsync(ms);
        string base64Image = Convert.ToBase64String(ms.ToArray());

        const string prompt = """
            You are a nutrition data extraction engine. I am providing you with a photo of a meal or food item.
            Identify all food items visible, estimate reasonable portions, and determine the meal type.
            
            OUTPUT ONLY THE RAW JSON FORMATTED OBJECT. Do not include any conversational text, greetings, markdown blocks (like ```json), or explanatory words before or after the JSON. The very first character of your response MUST be '{' and the last MUST be '}'.
            
            {
                "mealType": "Morning | Lunch | Evening | Dinner | Snack",
                "items": [
                    {
                        "foodName": "name of the food item",
                        "quantity": "estimated quantity with unit (e.g. '2 rotis', '1 bowl', '200ml')",
                        "estimatedCalories": 0
                    }
                ],
                "totalEstimatedCalories": 0,
                "confidence": "high | medium | low"
            }
            """;

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
            temperature = 0.1
        };

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", aiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        string fullUrl = aiEndpoint;
        if (!fullUrl.EndsWith("/chat/completions"))
            fullUrl = fullUrl.TrimEnd('/') + "/chat/completions";

        var response = await client.PostAsync(fullUrl, content);
        string responseJson = await response.Content.ReadAsStringAsync();

        try
        {
            using JsonDocument doc = JsonDocument.Parse(responseJson);
            string result = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "{}";

            // Clean markdown fences
            result = result.Trim();
            if (result.StartsWith("```json")) result = result[7..];
            else if (result.StartsWith("```")) result = result[3..];
            if (result.EndsWith("```")) result = result[..^3];

            int firstBrace = result.IndexOf('{');
            int lastBrace = result.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace >= firstBrace)
                result = result[firstBrace..(lastBrace + 1)];

            return JsonSerializer.Deserialize<MealVisionResult>(result.Trim(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? new MealVisionResult();
        }
        catch
        {
            return new MealVisionResult();
        }
    }
}

/// <summary>
/// Structured result from Vision LLM meal extraction.
/// </summary>
public class MealVisionResult
{
    public string MealType { get; set; } = "Snack";
    public List<MealVisionItem> Items { get; set; } = new();
    public int TotalEstimatedCalories { get; set; }
    public string Confidence { get; set; } = "low";
}

public class MealVisionItem
{
    public string FoodName { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public int EstimatedCalories { get; set; }
}
