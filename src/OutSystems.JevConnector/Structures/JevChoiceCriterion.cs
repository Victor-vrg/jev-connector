using OutSystems.ExternalLibraries.SDK;

namespace VRG.Jev.Structures;

/// <summary>
/// One option of a Choice question: the option's key and the rubric text that describes it.
/// Use <c>null</c> or an empty <see cref="Description"/> when the option is self-explanatory.
/// </summary>
[OSStructure(Description = "One option of a Jev Choice question: its key and the rubric text describing it.")]
public struct JevChoiceCriterion
{
    [OSStructureField(Description = "The option key, e.g. \"billing\". Returned verbatim in the answer.", IsMandatory = true)]
    public string Option { get; set; }

    [OSStructureField(Description = "Rubric text describing when this option applies. Leave empty if the option is self-explanatory.", IsMandatory = false)]
    public string Description { get; set; }
}
