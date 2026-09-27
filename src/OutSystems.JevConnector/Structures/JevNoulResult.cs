using OutSystems.ExternalLibraries.SDK;

namespace VRG.Jev.Structures;

/// <summary>
/// Result of a Noul (yes/no) evaluation. Check <see cref="Success"/> before reading
/// <see cref="Noul"/>.
/// </summary>
[OSStructure(Description = "Result of a Jev Noul (yes/no) evaluation. Check Success before reading Noul.")]
public struct JevNoulResult
{
    [OSStructureField(Description = "True when the call succeeded. When False, read ErrorCode and ErrorMessage: Noul is 0, which is NOT an answer of \"no\" - always branch on this field first.", IsMandatory = true)]
    public bool Success { get; set; }

    [OSStructureField(Description = "Stable token to branch on when Success is False: Validation, Unauthorized, InvalidRequest, RateLimit, Overloaded, Network, BadResponse, HttpError, Unexpected. Empty when Success is True. Each one is explained in the connector README.", IsMandatory = true)]
    public string ErrorCode { get; set; }

    [OSStructureField(Description = "Human-readable reason the call failed, including TypeSafe's own error detail. Empty when Success is True. Never contains your API key.", IsMandatory = true)]
    public string ErrorMessage { get; set; }

    [OSStructureField(Description = "HTTP status returned by the Jev API, or 0 when the call never reached it (local validation or network failure). Mapped: 401 invalid key, 422 rejected body, 429 rate limited, 529 overloaded. Full list in the connector README.", IsMandatory = true)]
    public int StatusCode { get; set; }

    [OSStructureField(Description = "The versioned model that answered, e.g. \"jev-1.13.0\". Empty when Success is False.", IsMandatory = true)]
    public string Model { get; set; }

    [OSStructureField(Description = "Probability the answer is yes, from 0 (no) to 1 (yes). Apply your own threshold for a boolean decision. Only meaningful when Success is True.", IsMandatory = true)]
    public double Noul { get; set; }

    [OSStructureField(Description = "Input tokens billed for this call.", IsMandatory = true)]
    public int InputTokens { get; set; }

    [OSStructureField(Description = "Output tokens billed for this call (Jev's output tokens are free of charge).", IsMandatory = true)]
    public int OutputTokens { get; set; }
}
