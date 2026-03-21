using System.Text.Json;
using System.Text.Json.Serialization;

namespace FitnessAgentsWeb.Models;

/// <summary>Converts both JSON strings and numbers to string.</summary>
public class FlexStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Number ? reader.GetDouble().ToString() : reader.GetString() ?? string.Empty;

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}

public class WorkoutPlan
{
    [JsonPropertyName("plan_date")]
    public string PlanDate { get; set; } = string.Empty;

    [JsonPropertyName("session_title")]
    public string SessionTitle { get; set; } = string.Empty;

    [JsonPropertyName("personalized_introduction")]
    public string PersonalizedIntroduction { get; set; } = string.Empty;

    [JsonPropertyName("warmup")]
    public List<WarmupExercise> Warmup { get; set; } = new();

    [JsonPropertyName("main_workout")]
    public List<MainExercise> MainWorkout { get; set; } = new();

    [JsonPropertyName("cooldown")]
    public List<CooldownExercise> Cooldown { get; set; } = new();

    [JsonPropertyName("coach_notes")]
    public string CoachNotes { get; set; } = string.Empty;

    /// <summary>Returns a flat list of all exercise names (warmup + main + cooldown).</summary>
    public List<string> GetAllExerciseNames()
    {
        var names = new List<string>();
        names.AddRange(Warmup.Select(e => e.Exercise).Where(n => !string.IsNullOrWhiteSpace(n)));
        names.AddRange(MainWorkout.Select(e => e.Exercise).Where(n => !string.IsNullOrWhiteSpace(n)));
        names.AddRange(Cooldown.Select(e => e.Exercise).Where(n => !string.IsNullOrWhiteSpace(n)));
        return names;
    }
}

public class WarmupExercise
{
    [JsonPropertyName("exercise")]
    public string Exercise { get; set; } = string.Empty;

    [JsonPropertyName("instruction")]
    public string Instruction { get; set; } = string.Empty;
}

public class MainExercise
{
    [JsonPropertyName("exercise")]
    public string Exercise { get; set; } = string.Empty;

    [JsonPropertyName("sets")]
    [JsonConverter(typeof(FlexStringConverter))]
    public string Sets { get; set; } = string.Empty;

    [JsonPropertyName("reps")]
    [JsonConverter(typeof(FlexStringConverter))]
    public string Reps { get; set; } = string.Empty;

    [JsonPropertyName("rest")]
    [JsonConverter(typeof(FlexStringConverter))]
    public string Rest { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;
}

public class CooldownExercise
{
    [JsonPropertyName("exercise")]
    public string Exercise { get; set; } = string.Empty;

    [JsonPropertyName("duration")]
    public string Duration { get; set; } = string.Empty;
}
