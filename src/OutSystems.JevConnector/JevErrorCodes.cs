namespace VRG.Jev;

/// <summary>
/// Stable tokens returned in the <c>ErrorCode</c> field of every result structure.
/// They are part of the connector's public contract: OutSystems flows branch on them,
/// so treat any change here as a breaking change. Documented in full in the README.
/// </summary>
internal static class JevErrorCodes
{
    /// <summary>A parameter was rejected locally; no HTTP call was made.</summary>
    public const string Validation = "Validation";

    /// <summary>HTTP 401: missing or invalid API key.</summary>
    public const string Unauthorized = "Unauthorized";

    /// <summary>HTTP 422: the API rejected the request body.</summary>
    public const string InvalidRequest = "InvalidRequest";

    /// <summary>HTTP 429: rate limited, and the retries were exhausted.</summary>
    public const string RateLimit = "RateLimit";

    /// <summary>HTTP 529: the API is overloaded, and the retries were exhausted.</summary>
    public const string Overloaded = "Overloaded";

    /// <summary>The API was unreachable or timed out, and the retries were exhausted.</summary>
    public const string Network = "Network";

    /// <summary>The API answered successfully but the body was missing, unparseable, or the wrong shape.</summary>
    public const string BadResponse = "BadResponse";

    /// <summary>Any other non-success HTTP status.</summary>
    public const string HttpError = "HttpError";

    /// <summary>An unforeseen failure inside the connector itself.</summary>
    public const string Unexpected = "Unexpected";
}
