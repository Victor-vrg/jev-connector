using System.Collections.Generic;
using OutSystems.ExternalLibraries.SDK;
using VRG.Jev.Structures;

namespace VRG.Jev;

/// <summary>
/// Calls TypeSafe AI's Jev (System One) evaluation endpoint from OutSystems.
/// Jev does not generate text: it evaluates a "state" against typed questions
/// (Noul, Choice, Score) and returns calibrated probabilities instead of prose.
/// See https://docs.typesafe.ai/api for the underlying HTTP contract.
/// <para>
/// None of these actions raise an exception. Every one returns a structure whose
/// <c>Success</c> field says whether the call worked, with <c>ErrorCode</c>,
/// <c>ErrorMessage</c> and <c>StatusCode</c> describing the failure when it didn't.
/// Always branch on <c>Success</c> before reading the rest of the result.
/// </para>
/// </summary>
[OSInterface(
    Name = "JevClient",
    Description = "Typed decisions from TypeSafe AI's Jev (System One) model: yes/no probabilities, classification and scoring, without writing prose.",
    IconResourceName = "VRG.Jev.resources.jev.png")]
public interface IJevClient
{
    /// <summary>
    /// Evaluates a single yes/no (Noul) question against a state and returns the
    /// probability the answer is yes.
    /// </summary>
    [OSAction(Description = "Evaluates a yes/no question against a state (e.g. \"Does this message convey urgency?\") and returns the probability the answer is yes, from 0 to 1. Never raises an exception: check Success before reading Noul.", IconResourceName = "VRG.Jev.resources.jev.png")]
    JevNoulResult EvaluateNoul(
        [OSParameter(Description = "Your TypeSafe API key (Authorization: Bearer). Get one at typesafe.ai; Jev is in early access behind a waitlist as of Sep 2026.")] string apiKey,
        [OSParameter(Description = "The content to evaluate: plain text, a chat transcript, a support ticket body, or any JSON encoded as text.")] string state,
        [OSParameter(Description = "The yes/no question to evaluate, e.g. \"Does this convey urgency?\"")] string instructions,
        [OSParameter(Description = "Optional: what a \"yes\" answer means. Leave empty to let Jev infer it from the question alone.")] string criteriaTrue = "",
        [OSParameter(Description = "Optional: what a \"no\" answer means. Leave empty to let Jev infer it from the question alone.")] string criteriaFalse = "",
        [OSParameter(Description = "Model alias to use. Defaults to \"jev-latest\", TypeSafe's flagship model.")] string model = "jev-latest",
        [OSParameter(Description = "Base URL of the evaluation API. Override to point at a gateway/proxy (e.g. an internal LiteLLM or Opper deployment) instead of TypeSafe directly.")] string apiBaseUrl = "https://api.typesafe.ai");

    /// <summary>
    /// Evaluates a Choice question: picks the best-fitting option from a set you define.
    /// </summary>
    [OSAction(Description = "Picks one option from up to 255 you define (e.g. routing a ticket to \"billing\"/\"technical\"/\"sales\") and returns the chosen option with the full probability distribution. Never raises an exception: check Success before reading Choice.", IconResourceName = "VRG.Jev.resources.jev.png")]
    JevChoiceResult EvaluateChoice(
        [OSParameter(Description = "Your TypeSafe API key (Authorization: Bearer).")] string apiKey,
        [OSParameter(Description = "The content to evaluate.")] string state,
        [OSParameter(Description = "What Jev should decide, e.g. \"Which team should handle this?\"")] string instructions,
        [OSParameter(Description = "The options to choose from, each with an optional rubric description. 1 to 255 entries.")] List<JevChoiceCriterion> options,
        [OSParameter(Description = "Model alias to use. Defaults to \"jev-latest\".")] string model = "jev-latest",
        [OSParameter(Description = "Base URL of the evaluation API.")] string apiBaseUrl = "https://api.typesafe.ai");

    /// <summary>
    /// Evaluates a Score question: rates the state along an ordered rubric.
    /// </summary>
    [OSAction(Description = "Rates the state along an ordered rubric you define (2 to 10 levels, e.g. \"Calm\" / \"Frustrated\" / \"Very angry\") and returns a probability-weighted score. Never raises an exception: check Success before reading Score.", IconResourceName = "VRG.Jev.resources.jev.png")]
    JevScoreResult EvaluateScore(
        [OSParameter(Description = "Your TypeSafe API key (Authorization: Bearer).")] string apiKey,
        [OSParameter(Description = "The content to evaluate.")] string state,
        [OSParameter(Description = "What Jev should rate, e.g. \"How frustrated is the customer?\"")] string instructions,
        [OSParameter(Description = "Ordered rubric levels, low to high, e.g. [\"Calm\", \"Frustrated\", \"Very angry\"]. 2 to 10 entries.")] List<string> levels,
        [OSParameter(Description = "Model alias to use. Defaults to \"jev-latest\".")] string model = "jev-latest",
        [OSParameter(Description = "Base URL of the evaluation API.")] string apiBaseUrl = "https://api.typesafe.ai");

    /// <summary>
    /// Advanced escape hatch: sends one or more raw, hand-built questions in a single
    /// call and returns the raw answers as JSON. Use this for structured state/instructions,
    /// batching several questions in one round trip, or any shape the typed actions don't cover.
    /// </summary>
    [OSAction(Description = "Advanced: sends one or more hand-built questions (raw JSON, matching the TypeSafe API's \"questions\" object) in a single call and returns the raw \"answers\" JSON. Use this for structured/JSON state, multi-question batches, or nested instructions the typed actions don't cover. Never raises an exception: check Success before reading AnswersJson.", IconResourceName = "VRG.Jev.resources.jev.png")]
    JevRawResult EvaluateRaw(
        [OSParameter(Description = "Your TypeSafe API key (Authorization: Bearer).")] string apiKey,
        [OSParameter(Description = "The content to evaluate. Plain text is sent as-is; text that parses as JSON (an object or array) is sent as structured state.")] string state,
        [OSParameter(Description = "The \"questions\" object as JSON text, e.g. {\"is_urgent\":{\"type\":\"noul\",\"instructions\":\"...\"}}. See https://docs.typesafe.ai/api.")] string questionsJson,
        [OSParameter(Description = "Model alias to use. Defaults to \"jev-latest\".")] string model = "jev-latest",
        [OSParameter(Description = "Base URL of the evaluation API.")] string apiBaseUrl = "https://api.typesafe.ai");
}
