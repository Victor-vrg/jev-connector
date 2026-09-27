using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VRG.Jev.Internal;

/// <summary>Top-level request body for POST /v1/systemone.</summary>
internal sealed class RequestDto
{
    [JsonPropertyName("state")]
    public object State { get; set; } = "";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("questions")]
    public Dictionary<string, QuestionDto> Questions { get; set; } = new();
}

/// <summary>A single typed question inside the "questions" map.</summary>
internal sealed class QuestionDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("instructions")]
    public object Instructions { get; set; } = "";

    [JsonPropertyName("criteria")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Criteria { get; set; }
}

/// <summary>Noul criteria shape: { "true": "...", "false": "..." }.</summary>
internal sealed class NoulCriteriaDto
{
    [JsonPropertyName("true")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? True { get; set; }

    [JsonPropertyName("false")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? False { get; set; }
}

/// <summary>Token usage, present on every successful response.</summary>
internal sealed class UsageDto
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }
}

/// <summary>Top-level response body. Answers are kept as raw JsonElement and parsed per-question-type by the caller.</summary>
internal sealed class ResponseDto
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("answers")]
    public Dictionary<string, JsonElement> Answers { get; set; } = new();

    [JsonPropertyName("usage")]
    public UsageDto Usage { get; set; } = new();
}

/// <summary>Best-effort shape of a TypeSafe error body: { "message": "..." } (or similar).</summary>
internal sealed class ErrorBodyDto
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

internal static class JevJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };
}
