using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace FitnessAgentsWeb.Models;

public record InBodyExport
{
    [JsonPropertyName("scan_date")] public string ScanDate { get; init; }
    [JsonPropertyName("core_composition")] public CoreComposition Core { get; init; }
    [JsonPropertyName("segmental_lean_analysis")] public SegmentalLean LeanBalance { get; init; }
    [JsonPropertyName("inbody_targets")] public InBodyTargets Targets { get; init; }
    [JsonPropertyName("metabolic_health")] public MetabolicHealth Metabolism { get; init; }
}

public record CoreComposition
{
    [JsonPropertyName("weight_kg")] public double WeightKg { get; init; }
    [JsonPropertyName("skeletal_muscle_mass_kg")] public double SmmKg { get; init; }
    [JsonPropertyName("percent_body_fat")] public double Pbf { get; init; }
}

public record SegmentalLean
{
    [JsonPropertyName("right_leg_evaluation")] public string RightLeg { get; init; }
    [JsonPropertyName("left_leg_evaluation")] public string LeftLeg { get; init; }
    [JsonPropertyName("trunk_evaluation")] public string Trunk { get; init; }
    [JsonPropertyName("right_arm_evaluation")] public string RightArm { get; init; }
    [JsonPropertyName("left_arm_evaluation")] public string LeftArm { get; init; }
}

public record InBodyTargets
{
    [JsonPropertyName("fat_control_kg")] public double FatControl { get; init; }
    [JsonPropertyName("muscle_control_kg")] public double MuscleControl { get; init; } 
}

public record MetabolicHealth
{
    [JsonPropertyName("basal_metabolic_rate_kcal")] public int Bmr { get; init; }
}
