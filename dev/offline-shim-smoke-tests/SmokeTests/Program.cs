using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VRG.Jev;
using VRG.Jev.Structures;
using JevConnector.ShimSmokeTests;

var failures = new List<string>();
var passed = 0;

void Check(string name, Action test)
{
    try
    {
        test();
        passed++;
        Console.WriteLine($"PASS  {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{name}: {ex}");
        Console.WriteLine($"FAIL  {name}\n      {ex.Message}");
    }
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception($"Assertion failed: {message}");
}

ILogger logger = new ConsoleLogger();

// ---------------------------------------------------------------------
Check("EvaluateNoul: happy path maps fields and sends correct request body", () =>
{
    var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK,
        """{"model":"jev-1.13.0","answers":{"result":{"type":"noul","noul":0.95}},"usage":{"input_tokens":307,"output_tokens":20}}""");
    var client = new JevClient(logger, new HttpClient(handler));

    var result = client.EvaluateNoul(
        apiKey: "sk-test",
        state: "Help! My payouts have been failing for 3 days.",
        instructions: "Does this convey urgency?",
        criteriaTrue: "Explicitly time-sensitive",
        criteriaFalse: "No urgency expressed");

    Assert(result.Model == "jev-1.13.0", "model");
    Assert(Math.Abs(result.Noul - 0.95) < 1e-9, "noul value");
    Assert(result.InputTokens == 307 && result.OutputTokens == 20, "usage");
    Assert(handler.Requests.Count == 1, "single request");

    var (method, url, body) = handler.Requests[0];
    Assert(method == "POST", "http method");
    Assert(url == "https://api.typesafe.ai/v1/systemone", "url");
    using var doc = JsonDocument.Parse(body);
    var root = doc.RootElement;
    Assert(root.GetProperty("model").GetString() == "jev-latest", "default model in body");
    Assert(root.GetProperty("state").GetString()!.Contains("payouts"), "state in body");
    var q = root.GetProperty("questions").GetProperty("result");
    Assert(q.GetProperty("type").GetString() == "noul", "question type");
    Assert(q.GetProperty("instructions").GetString() == "Does this convey urgency?", "instructions");
    Assert(q.GetProperty("criteria").GetProperty("true").GetString() == "Explicitly time-sensitive", "criteria.true");
    Assert(q.GetProperty("criteria").GetProperty("false").GetString() == "No urgency expressed", "criteria.false");
});

Check("EvaluateNoul: omits criteria entirely when not supplied", () =>
{
    var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK,
        """{"model":"jev-1.13.0","answers":{"result":{"type":"noul","noul":0.1}},"usage":{"input_tokens":10,"output_tokens":5}}""");
    var client = new JevClient(logger, new HttpClient(handler));

    client.EvaluateNoul("sk-test", "some state", "Is this spam?");

    var (_, _, body) = handler.Requests[0];
    using var doc = JsonDocument.Parse(body);
    var q = doc.RootElement.GetProperty("questions").GetProperty("result");
    Assert(!q.TryGetProperty("criteria", out _), "criteria should be absent");
});

// ---------------------------------------------------------------------
Check("EvaluateChoice: happy path maps probabilities and confidence", () =>
{
    var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK,
        """{"model":"jev-1.13.0","answers":{"result":{"type":"choice","choice":"billing","probabilities":{"billing":0.88,"technical":0.12,"sales":0.0},"confidence":0.81}},"usage":{"input_tokens":318,"output_tokens":34}}""");
    var client = new JevClient(logger, new HttpClient(handler));

    var result = client.EvaluateChoice(
        apiKey: "sk-test",
        state: "Help! My payouts have been failing for 3 days.",
        instructions: "Which team should handle this?",
        options: new List<JevChoiceCriterion>
        {
            new() { Option = "billing", Description = "Payments, invoicing, refunds" },
            new() { Option = "technical", Description = "Bugs, outages, integrations" },
            new() { Option = "sales", Description = "Pricing, upgrades, new accounts" },
        });

    Assert(result.Choice == "billing", "choice");
    Assert(Math.Abs(result.Confidence - 0.81) < 1e-9, "confidence");
    Assert(result.Probabilities.Count == 3, "probabilities count");
    Assert(result.Probabilities.Single(p => p.Option == "billing").Probability == 0.88, "billing probability");

    var (_, _, body) = handler.Requests[0];
    using var doc = JsonDocument.Parse(body);
    var criteria = doc.RootElement.GetProperty("questions").GetProperty("result").GetProperty("criteria");
    Assert(criteria.GetProperty("billing").GetString() == "Payments, invoicing, refunds", "criteria mapped");
});

Check("EvaluateChoice: rejects empty options without calling the API", () =>
{
    var handler = new FakeHttpMessageHandler();
    var client = new JevClient(logger, new HttpClient(handler));
    var threw = false;
    try { client.EvaluateChoice("sk-test", "state", "question?", new List<JevChoiceCriterion>()); }
    catch (JevApiException) { threw = true; }
    Assert(threw, "should throw JevApiException");
    Assert(handler.Requests.Count == 0, "no HTTP call made");
});

Check("EvaluateChoice: null description serializes as JSON null", () =>
{
    var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK,
        """{"model":"jev-1.13.0","answers":{"result":{"type":"choice","choice":"a","probabilities":{"a":1.0},"confidence":1.0}},"usage":{"input_tokens":1,"output_tokens":1}}""");
    var client = new JevClient(logger, new HttpClient(handler));
    client.EvaluateChoice("sk-test", "state", "question?", new List<JevChoiceCriterion> { new() { Option = "a", Description = "" } });

    var (_, _, body) = handler.Requests[0];
    using var doc = JsonDocument.Parse(body);
    var criteria = doc.RootElement.GetProperty("questions").GetProperty("result").GetProperty("criteria");
    Assert(criteria.GetProperty("a").ValueKind == JsonValueKind.Null, "empty description -> JSON null");
});

// ---------------------------------------------------------------------
Check("EvaluateScore: happy path maps legend, probabilities and score", () =>
{
    var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK,
        """{"model":"jev-1.13.0","answers":{"result":{"type":"score","score":1.05,"legend":{"0":"Calm","1":"Frustrated","2":"Very angry"},"probabilities":{"0":0.0,"1":0.95,"2":0.05},"confidence":0.92}},"usage":{"input_tokens":304,"output_tokens":18}}""");
    var client = new JevClient(logger, new HttpClient(handler));

    var result = client.EvaluateScore(
        apiKey: "sk-test",
        state: "Help! My payouts have been failing for 3 days.",
        instructions: "How frustrated is the customer?",
        levels: new List<string> { "Calm", "Frustrated", "Very angry" });

    Assert(Math.Abs(result.Score - 1.05) < 1e-9, "score");
    Assert(Math.Abs(result.Confidence - 0.92) < 1e-9, "confidence");
    Assert(result.Levels.Count == 3, "levels count");
    Assert(result.Levels[0].Level == 0 && result.Levels[0].Description == "Calm", "level 0 legend");
    Assert(result.Levels[1].Probability == 0.95, "level 1 probability");
    // must be sorted ascending by level
    Assert(result.Levels[0].Level < result.Levels[1].Level && result.Levels[1].Level < result.Levels[2].Level, "sorted ascending");
});

Check("EvaluateScore: rejects fewer than 2 levels and more than 10", () =>
{
    var handler = new FakeHttpMessageHandler();
    var client = new JevClient(logger, new HttpClient(handler));

    var threwTooFew = false;
    try { client.EvaluateScore("sk-test", "s", "q?", new List<string> { "only-one" }); }
    catch (JevApiException) { threwTooFew = true; }
    Assert(threwTooFew, "should reject 1 level");

    var elevenLevels = Enumerable.Range(0, 11).Select(i => $"L{i}").ToList();
    var threwTooMany = false;
    try { client.EvaluateScore("sk-test", "s", "q?", elevenLevels); }
    catch (JevApiException) { threwTooMany = true; }
    Assert(threwTooMany, "should reject 11 levels");

    Assert(handler.Requests.Count == 0, "no HTTP call made for either");
});

// ---------------------------------------------------------------------
Check("EvaluateRaw: batches multiple questions and round-trips answers JSON", () =>
{
    var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK,
        """{"model":"jev-1.13.0","answers":{"is_lead":{"type":"noul","noul":0.91},"urgency":{"type":"score","score":1.4,"legend":{"0":"Low","1":"Medium","2":"High"},"probabilities":{"0":0.1,"1":0.4,"2":0.5},"confidence":0.6}},"usage":{"input_tokens":500,"output_tokens":40}}""");
    var client = new JevClient(logger, new HttpClient(handler));

    var questionsJson = """
    {
      "is_lead": {"type":"noul","instructions":"Is this a sales lead?"},
      "urgency": {"type":"score","instructions":"How urgent?","criteria":["Low","Medium","High"]}
    }
    """;

    var result = client.EvaluateRaw("sk-test", "Customer emailed about pricing.", questionsJson);

    Assert(result.Model == "jev-1.13.0", "model");
    Assert(result.InputTokens == 500 && result.OutputTokens == 40, "usage");
    using var answers = JsonDocument.Parse(result.AnswersJson);
    Assert(answers.RootElement.GetProperty("is_lead").GetProperty("noul").GetDouble() == 0.91, "is_lead answer round-trips");
    Assert(answers.RootElement.GetProperty("urgency").GetProperty("score").GetDouble() == 1.4, "urgency answer round-trips");

    var (_, _, body) = handler.Requests[0];
    using var reqDoc = JsonDocument.Parse(body);
    Assert(reqDoc.RootElement.GetProperty("questions").GetProperty("is_lead").GetProperty("type").GetString() == "noul", "raw questions passed through");
});

Check("EvaluateRaw: state that parses as JSON is sent as structured state, not a quoted string", () =>
{
    var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK,
        """{"model":"jev-1.13.0","answers":{"q":{"type":"noul","noul":0.5}},"usage":{"input_tokens":1,"output_tokens":1}}""");
    var client = new JevClient(logger, new HttpClient(handler));

    client.EvaluateRaw("sk-test", """{"messages":[{"role":"user","content":"hi"}]}""", """{"q":{"type":"noul","instructions":"x"}}""");

    var (_, _, body) = handler.Requests[0];
    using var doc = JsonDocument.Parse(body);
    var state = doc.RootElement.GetProperty("state");
    Assert(state.ValueKind == JsonValueKind.Object, "state should be a JSON object, not a string");
    Assert(state.GetProperty("messages")[0].GetProperty("role").GetString() == "user", "nested state preserved");
});

Check("EvaluateRaw: plain-text state stays a JSON string", () =>
{
    var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK,
        """{"model":"jev-1.13.0","answers":{"q":{"type":"noul","noul":0.5}},"usage":{"input_tokens":1,"output_tokens":1}}""");
    var client = new JevClient(logger, new HttpClient(handler));

    client.EvaluateRaw("sk-test", "just plain text, not json", """{"q":{"type":"noul","instructions":"x"}}""");

    var (_, _, body) = handler.Requests[0];
    using var doc = JsonDocument.Parse(body);
    Assert(doc.RootElement.GetProperty("state").ValueKind == JsonValueKind.String, "state should stay a JSON string");
});

Check("EvaluateRaw: invalid questionsJson throws without calling the API", () =>
{
    var handler = new FakeHttpMessageHandler();
    var client = new JevClient(logger, new HttpClient(handler));
    var threw = false;
    try { client.EvaluateRaw("sk-test", "state", "{not valid json"); }
    catch (JevApiException) { threw = true; }
    Assert(threw, "should throw JevApiException");
    Assert(handler.Requests.Count == 0, "no HTTP call made");
});

// ---------------------------------------------------------------------
Check("Errors: 401 fails immediately without retry", () =>
{
    var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.Unauthorized, """{"message":"Invalid API key"}""");
    var client = new JevClient(logger, new HttpClient(handler));

    JevApiException? caught = null;
    try { client.EvaluateNoul("bad-key", "s", "q?"); }
    catch (JevApiException ex) { caught = ex; }

    Assert(caught is not null, "should throw");
    Assert(caught!.StatusCode == 401, "status code 401");
    Assert(caught.Message.Contains("Invalid API key"), "message includes API detail");
    Assert(handler.Requests.Count == 1, "no retry on 401");
});

Check("Errors: 422 fails immediately without retry", () =>
{
    var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.UnprocessableEntity, """{"message":"criteria must have at least 2 levels"}""");
    var client = new JevClient(logger, new HttpClient(handler));

    JevApiException? caught = null;
    try { client.EvaluateScore("sk-test", "s", "q?", new List<string> { "a", "b" }); }
    catch (JevApiException ex) { caught = ex; }

    Assert(caught is not null, "should throw");
    Assert(caught!.StatusCode == 422, "status code 422");
    Assert(handler.Requests.Count == 1, "no retry on 422");
});

Check("Retries: 429 then 429 then success on 3rd attempt", () =>
{
    var handler = new FakeHttpMessageHandler()
        .Enqueue((HttpStatusCode)429, """{"message":"rate limited"}""", TimeSpan.FromMilliseconds(5))
        .Enqueue((HttpStatusCode)429, """{"message":"rate limited"}""", TimeSpan.FromMilliseconds(5))
        .Enqueue(HttpStatusCode.OK, """{"model":"jev-1.13.0","answers":{"result":{"type":"noul","noul":0.7}},"usage":{"input_tokens":1,"output_tokens":1}}""");
    var client = new JevClient(logger, new HttpClient(handler));

    var result = client.EvaluateNoul("sk-test", "s", "q?");

    Assert(Math.Abs(result.Noul - 0.7) < 1e-9, "eventual success value");
    Assert(handler.Requests.Count == 3, "retried twice then succeeded");
});

Check("Retries: 529 exhausted after max attempts throws", () =>
{
    var handler = new FakeHttpMessageHandler();
    for (var i = 0; i < 4; i++)
    {
        handler.Enqueue((HttpStatusCode)529, """{"message":"overloaded"}""", TimeSpan.FromMilliseconds(5));
    }
    var client = new JevClient(logger, new HttpClient(handler));

    JevApiException? caught = null;
    try { client.EvaluateNoul("sk-test", "s", "q?"); }
    catch (JevApiException ex) { caught = ex; }

    Assert(caught is not null, "should throw after exhausting retries");
    Assert(caught!.StatusCode == 529, "status code 529");
    Assert(handler.Requests.Count == 4, "exactly MaxAttempts requests were made");
});

// ---------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine($"{passed} passed, {failures.Count} failed.");
if (failures.Count > 0)
{
    Console.WriteLine("Failures:");
    foreach (var f in failures) Console.WriteLine($" - {f}");
    return 1;
}
return 0;
