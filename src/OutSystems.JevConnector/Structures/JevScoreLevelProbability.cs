using OutSystems.ExternalLibraries.SDK;

namespace VRG.Jev.Structures;

/// <summary>
/// The probability Jev assigned to a single Score level.
/// </summary>
[OSStructure(Description = "The probability Jev assigned to one Score level (rubric rung).")]
public struct JevScoreLevelProbability
{
    [OSStructureField(Description = "Zero-based index of this level, matching the order of the Levels list sent in the request.", IsMandatory = true)]
    public int Level { get; set; }

    [OSStructureField(Description = "The rubric text for this level, as sent in the request.", IsMandatory = true)]
    public string Description { get; set; }

    [OSStructureField(Description = "Probability of this level, from 0 to 1. All levels in the answer sum to 1.", IsMandatory = true)]
    public double Probability { get; set; }
}
