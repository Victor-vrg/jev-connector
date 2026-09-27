using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VRG.Jev.Internal;
using VRG.Jev.Structures;

namespace VRG.Jev;

/// <summary>
/// Default implementation of <see cref="IJevClient"/>. Instantiated by ODC per call;
/// <see cref="ILogger"/> is injected automatically so calls show up in ODC Portal logs.
/// <para>
/// No action throws: every failure comes back as <c>Success = false</c> with an
/// <c>ErrorCode</c>, an <c>ErrorMessage</c> and a <c>StatusCode</c> on the result structure,
/// so OutSystems flows can branch on it. Failures are still logged for the ODC Portal.
/// </para>
/// </summary>
public sealed class JevClient : IJevClient
{
    private const int MaxAttempts = 4;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromMilliseconds(400);

    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;

    public JevClient(ILogger logger) : this(logger, CreateDefaultHttpClient())
    {
    }

    /// <summary>Test-only constructor: lets unit tests inject a fake <see cref="HttpClient"/>.</summary>
    internal JevClient(ILogger logger, HttpClient httpClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    private static HttpClient CreateDefaultHttpClient() => new() { Timeout = TimeSpan.FromSeconds(30) };

    public JevNoulResult EvaluateNoul(
        string apiKey,
        string state,
        string instructions,
        string criteriaTrue = "",
        string criteriaFalse = "",
        string model = "jev-latest",
        string apiBaseUrl = "https://api.typesafe.ai")
        => Execute(
            nameof(EvaluateNoul),
            () => EvaluateNoulCore(apiKey, state, instructions, criteriaTrue, criteriaFalse, model, apiBaseUrl),
            failure => new JevNoulResult
            {
                Success = false,
                ErrorCode = failure.ErrorCode,
                ErrorMessage = failure.Message,
                StatusCode = failure.StatusCode,
                Model = "",
                Noul = 0,
                InputTokens = 0,
                OutputTokens = 0,
            });

    private JevNoulResult EvaluateNoulCore(
        string apiKey,
        string state,
        string instructions,
        string criteriaTrue,
        string criteriaFalse,
        string model,
        string apiBaseUrl)
    {
        ValidateCommon(apiKey, state, instructions, model, apiBaseUrl);

        var question = new QuestionDto { Type = "noul", Instructions = instructions };
        if (!string.IsNullOrEmpty(criteriaTrue) || !string.IsNullOrEmpty(criteriaFalse))
        {
            question.Criteria = new NoulCriteriaDto
            {
                True = string.IsNullOrEmpty(criteriaTrue) ? null : criteriaTrue,
                False = string.IsNullOrEmpty(criteriaFalse) ? null : criteriaFalse,
            };
        }

        var requestJson = BuildTypedRequestJson(state, model, question);
        var (response, statusCode) = SendAsync(requestJson, apiKey, apiBaseUrl).GetAwaiter().GetResult();
        var answer = GetAnswer(response, "result");

        return new JevNoulResult
        {
            Success = true,
            ErrorCode = "",
            ErrorMessage = "",
            StatusCode = statusCode,
            Model = response.Model,
            Noul = answer.GetProperty("noul").GetDouble(),
            InputTokens = response.Usage.InputTokens,
            OutputTokens = response.Usage.OutputTokens,
        };
    }

    public JevChoiceResult EvaluateChoice(
        string apiKey,
        string state,
        string instructions,
        List<JevChoiceCriterion> options,
        string model = "jev-latest",
        string apiBaseUrl = "https://api.typesafe.ai")
        => Execute(
            nameof(EvaluateChoice),
            () => EvaluateChoiceCore(apiKey, state, instructions, options, model, apiBaseUrl),
            failure => new JevChoiceResult
            {
                Success = false,
                ErrorCode = failure.ErrorCode,
                ErrorMessage = failure.Message,
                StatusCode = failure.StatusCode,
                Model = "",
                Choice = "",
                Confidence = 0,
                Probabilities = new List<JevChoiceProbability>(),
                InputTokens = 0,
                OutputTokens = 0,
            });

    private JevChoiceResult EvaluateChoiceCore(
        string apiKey,
        string state,
        string instructions,
        List<JevChoiceCriterion> options,
        string model,
        string apiBaseUrl)
    {
        ValidateCommon(apiKey, state, instructions, model, apiBaseUrl);

        if (options is null || options.Count == 0)
        {
            throw Invalid("EvaluateChoice requires at least one option in \"options\".");
        }
        if (options.Count > 255)
        {
            throw Invalid("EvaluateChoice accepts at most 255 options (Jev's own limit).");
        }

        var criteria = new Dictionary<string, object?>();
        foreach (var option in options)
        {
            if (string.IsNullOrWhiteSpace(option.Option))
            {
                throw Invalid("Every entry in \"options\" must have a non-empty Option key.");
            }
            criteria[option.Option] = string.IsNullOrEmpty(option.Description) ? null : option.Description;
        }

        var question = new QuestionDto { Type = "choice", Instructions = instructions, Criteria = criteria };
        var requestJson = BuildTypedRequestJson(state, model, question);
        var (response, statusCode) = SendAsync(requestJson, apiKey, apiBaseUrl).GetAwaiter().GetResult();
        var answer = GetAnswer(response, "result");

        var probabilities = new List<JevChoiceProbability>();
        foreach (var prop in answer.GetProperty("probabilities").EnumerateObject())
        {
            probabilities.Add(new JevChoiceProbability { Option = prop.Name, Probability = prop.Value.GetDouble() });
        }

        return new JevChoiceResult
        {
            Success = true,
            ErrorCode = "",
            ErrorMessage = "",
            StatusCode = statusCode,
            Model = response.Model,
            Choice = answer.GetProperty("choice").GetString() ?? "",
            Confidence = answer.GetProperty("confidence").GetDouble(),
            Probabilities = probabilities,
            InputTokens = response.Usage.InputTokens,
            OutputTokens = response.Usage.OutputTokens,
        };
    }

    public JevScoreResult EvaluateScore(
        string apiKey,
        string state,
        string instructions,
        List<string> levels,
        string model = "jev-latest",
        string apiBaseUrl = "https://api.typesafe.ai")
        => Execute(
            nameof(EvaluateScore),
            () => EvaluateScoreCore(apiKey, state, instructions, levels, model, apiBaseUrl),
            failure => new JevScoreResult
            {
                Success = false,
                ErrorCode = failure.ErrorCode,
                ErrorMessage = failure.Message,
                StatusCode = failure.StatusCode,
                Model = "",
                Score = 0,
                Confidence = 0,
                Levels = new List<JevScoreLevelProbability>(),
                InputTokens = 0,
                OutputTokens = 0,
            });

    private JevScoreResult EvaluateScoreCore(
        string apiKey,
        string state,
        string instructions,
        List<string> levels,
        string model,
        string apiBaseUrl)
    {
        ValidateCommon(apiKey, state, instructions, model, apiBaseUrl);

        if (levels is null || levels.Count < 2)
        {
            throw Invalid("EvaluateScore requires at least two ordered entries in \"levels\".");
        }
        if (levels.Count > 10)
        {
            throw Invalid("EvaluateScore accepts at most 10 levels (Jev's own limit).");
        }

        var question = new QuestionDto { Type = "score", Instructions = instructions, Criteria = levels };
        var requestJson = BuildTypedRequestJson(state, model, question);
        var (response, statusCode) = SendAsync(requestJson, apiKey, apiBaseUrl).GetAwaiter().GetResult();
        var answer = GetAnswer(response, "result");

        var legend = new Dictionary<int, string>();
        foreach (var prop in answer.GetProperty("legend").EnumerateObject())
        {
            if (int.TryParse(prop.Name, out var levelIndex))
            {
                legend[levelIndex] = prop.Value.GetString() ?? "";
            }
        }

        var levelResults = new List<JevScoreLevelProbability>();
        foreach (var prop in answer.GetProperty("probabilities").EnumerateObject())
        {
            if (!int.TryParse(prop.Name, out var levelIndex))
            {
                continue;
            }
            levelResults.Add(new JevScoreLevelProbability
            {
                Level = levelIndex,
                Description = legend.TryGetValue(levelIndex, out var desc) ? desc : "",
                Probability = prop.Value.GetDouble(),
            });
        }
        levelResults.Sort((a, b) => a.Level.CompareTo(b.Level));

        return new JevScoreResult
        {
            Success = true,
            ErrorCode = "",
            ErrorMessage = "",
            StatusCode = statusCode,
            Model = response.Model,
            Score = answer.GetProperty("score").GetDouble(),
            Confidence = answer.GetProperty("confidence").GetDouble(),
            Levels = levelResults,
            InputTokens = response.Usage.InputTokens,
            OutputTokens = response.Usage.OutputTokens,
        };
    }

    public JevRawResult EvaluateRaw(
        string apiKey,
        string state,
        string questionsJson,
        string model = "jev-latest",
        string apiBaseUrl = "https://api.typesafe.ai")
        => Execute(
            nameof(EvaluateRaw),
            () => EvaluateRawCore(apiKey, state, questionsJson, model, apiBaseUrl),
            failure => new JevRawResult
            {
                Success = false,
                ErrorCode = failure.ErrorCode,
                ErrorMessage = failure.Message,
                StatusCode = failure.StatusCode,
                Model = "",
                AnswersJson = "",
                InputTokens = 0,
                OutputTokens = 0,
            });

    private JevRawResult EvaluateRawCore(
        string apiKey,
        string state,
        string questionsJson,
        string model,
        string apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw Invalid("apiKey is required.");
        }
        if (string.IsNullOrWhiteSpace(questionsJson))
        {
            throw Invalid("questionsJson is required.");
        }
        if (string.IsNullOrWhiteSpace(model))
        {
            throw Invalid("model is required.");
        }
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            throw Invalid("apiBaseUrl is required.");
        }

        JsonNode questionsNode;
        try
        {
            questionsNode = JsonNode.Parse(questionsJson) ?? throw new JsonException("questionsJson parsed to null.");
        }
        catch (JsonException ex)
        {
            throw new JevApiException(JevErrorCodes.Validation, 0, $"questionsJson is not valid JSON: {ex.Message}", ex);
        }

        var root = new JsonObject
        {
            ["model"] = model,
            ["state"] = BuildStateNode(state),
            ["questions"] = questionsNode,
        };

        var requestJson = root.ToJsonString();
        var (response, statusCode) = SendAsync(requestJson, apiKey, apiBaseUrl).GetAwaiter().GetResult();
        var answersJson = JsonSerializer.Serialize(response.Answers, JevJson.Options);

        return new JevRawResult
        {
            Success = true,
            ErrorCode = "",
            ErrorMessage = "",
            StatusCode = statusCode,
            Model = response.Model,
            AnswersJson = answersJson,
            InputTokens = response.Usage.InputTokens,
            OutputTokens = response.Usage.OutputTokens,
        };
    }

    // ---- action boundary -------------------------------------------------

    /// <summary>
    /// Runs one action's body and converts any failure into a populated result structure.
    /// Nothing thrown in here reaches OutSystems: an ODC Exception Handler can only catch a
    /// generic error, which leaves the developer with no way to tell a bad API key from a
    /// rate limit. The failure is logged and then handed back as data instead.
    /// </summary>
    private T Execute<T>(string actionName, Func<T> core, Func<JevApiException, T> toFailure)
    {
        try
        {
            return core();
        }
        catch (JevApiException ex)
        {
            _logger.LogError(ex, "{Action} failed: {ErrorCode} (HTTP {StatusCode})", actionName, ex.ErrorCode, ex.StatusCode);
            return toFailure(ex);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            // The API answered, but the body wasn't the shape this connector expects.
            _logger.LogError(ex, "{Action} could not read the Jev API response", actionName);
            return toFailure(new JevApiException(
                JevErrorCodes.BadResponse,
                0,
                $"The Jev API response was not in the expected format: {ex.Message}",
                ex));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Action} failed unexpectedly", actionName);
            return toFailure(new JevApiException(
                JevErrorCodes.Unexpected,
                0,
                $"Unexpected failure in {actionName}: {ex.Message}",
                ex));
        }
    }

    // ---- shared plumbing -------------------------------------------------

    private static JevApiException Invalid(string message) =>
        new(JevErrorCodes.Validation, 0, message);

    private static string BuildTypedRequestJson(string state, string model, QuestionDto question)
    {
        var request = new RequestDto
        {
            State = state,
            Model = model,
            Questions = { ["result"] = question },
        };
        return JsonSerializer.Serialize(request, JevJson.Options);
    }

    private static JsonNode? BuildStateNode(string state)
    {
        if (string.IsNullOrEmpty(state))
        {
            return JsonValue.Create(state);
        }

        try
        {
            using var doc = JsonDocument.Parse(state);
            if (doc.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                return JsonNode.Parse(state);
            }
        }
        catch (JsonException)
        {
            // Not JSON; fall through and send as plain text.
        }

        return JsonValue.Create(state);
    }

    private static void ValidateCommon(string apiKey, string state, string instructions, string model, string apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw Invalid("apiKey is required.");
        }
        if (state is null)
        {
            throw Invalid("state is required.");
        }
        if (string.IsNullOrWhiteSpace(instructions))
        {
            throw Invalid("instructions is required.");
        }
        if (string.IsNullOrWhiteSpace(model))
        {
            throw Invalid("model is required.");
        }
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            throw Invalid("apiBaseUrl is required.");
        }
    }

    private static JsonElement GetAnswer(ResponseDto response, string id)
    {
        if (!response.Answers.TryGetValue(id, out var answer))
        {
            throw new JevApiException(
                JevErrorCodes.BadResponse,
                0,
                $"Jev API response did not include an answer for question \"{id}\".");
        }
        return answer;
    }

    private async Task<(ResponseDto Body, int StatusCode)> SendAsync(string requestJson, string apiKey, string apiBaseUrl)
    {
        var url = $"{apiBaseUrl.TrimEnd('/')}/v1/systemone";
        var delay = InitialRetryDelay;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(requestJson, Encoding.UTF8, "application/json"),
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                if (attempt == MaxAttempts)
                {
                    throw new JevApiException(
                        JevErrorCodes.Network,
                        0,
                        $"Could not reach the Jev API after {MaxAttempts} attempts: {ex.Message}",
                        ex);
                }
                _logger.LogWarning(ex, "Jev API call failed (attempt {Attempt}/{MaxAttempts}); retrying in {DelayMs}ms", attempt, MaxAttempts, delay.TotalMilliseconds);
                await Task.Delay(delay).ConfigureAwait(false);
                delay += delay;
                continue;
            }

            using (response)
            {
                var statusCode = (int)response.StatusCode;

                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var parsed = JsonSerializer.Deserialize<ResponseDto>(body, JevJson.Options)
                        ?? throw new JevApiException(
                            JevErrorCodes.BadResponse,
                            statusCode,
                            "Jev API returned an empty or unparseable response body.");
                    return (parsed, statusCode);
                }

                var isRetryable = statusCode == 429 || statusCode == 529;

                if (isRetryable && attempt < MaxAttempts)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta ?? delay;
                    _logger.LogWarning("Jev API returned HTTP {StatusCode} (attempt {Attempt}/{MaxAttempts}); retrying in {DelayMs}ms", statusCode, attempt, MaxAttempts, retryAfter.TotalMilliseconds);
                    await Task.Delay(retryAfter).ConfigureAwait(false);
                    delay += delay;
                    continue;
                }

                var errorBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                throw new JevApiException(ErrorCodeFor(statusCode), statusCode, BuildErrorMessage(statusCode, errorBody));
            }
        }

        throw new JevApiException(JevErrorCodes.Network, 0, "Could not reach the Jev API: retries exhausted.");
    }

    private static string ErrorCodeFor(int statusCode) => statusCode switch
    {
        401 => JevErrorCodes.Unauthorized,
        422 => JevErrorCodes.InvalidRequest,
        429 => JevErrorCodes.RateLimit,
        529 => JevErrorCodes.Overloaded,
        _ => JevErrorCodes.HttpError,
    };

    private static string BuildErrorMessage(int statusCode, string body)
    {
        string? detail = null;
        try
        {
            var parsed = JsonSerializer.Deserialize<ErrorBodyDto>(body, JevJson.Options);
            detail = parsed?.Message ?? parsed?.Error;
        }
        catch (JsonException)
        {
            // Body wasn't the expected JSON shape; fall back to the raw text below.
        }

        detail ??= body.Length > 500 ? body[..500] : body;

        return statusCode switch
        {
            401 => $"Jev API rejected the request: missing or invalid API key. {detail}",
            422 => $"Jev API rejected the request body (check instructions/criteria): {detail}",
            429 => $"Jev API rate limit exceeded and retries were exhausted: {detail}",
            529 => $"Jev API is temporarily overloaded and retries were exhausted: {detail}",
            _ => $"Jev API returned HTTP {statusCode}: {detail}",
        };
    }
}
