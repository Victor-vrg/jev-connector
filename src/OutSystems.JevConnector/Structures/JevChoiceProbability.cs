using OutSystems.ExternalLibraries.SDK;

namespace VRG.Jev.Structures;

/// <summary>
/// The probability Jev assigned to a single Choice option.
/// </summary>
[OSStructure(Description = "The probability Jev assigned to one Choice option.")]
public struct JevChoiceProbability
{
    [OSStructureField(Description = "The option key, as defined in the request criteria.", IsMandatory = true)]
    public string Option { get; set; }

    [OSStructureField(Description = "Probability of this option, from 0 to 1. All options in the answer sum to 1.", IsMandatory = true)]
    public double Probability { get; set; }
}
