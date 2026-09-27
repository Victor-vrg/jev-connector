using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using VRG.Jev.Structures;
using Xunit;

namespace VRG.Jev.Tests;

public class JevClientTests
{
    private static JevClient MakeClient(FakeHttpMessageHandler handler) =>
        new(NullLogger.Instance, new HttpClient(handler));

    [Fact]
    public void EvaluateNoul_HappyPath_MapsFieldsAndSendsCorrectBody()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK,
            """{"model":"jev-1.13.0","answers":{"result":{"type":"noul","noul":0.95}},"usage":{"input_tokens":307,"output_tokens":20}}""");
        var client = MakeClient(handler);

        var result = client.EvaluateNoul(
            apiKey: "sk-test",
            state: "Help! My payouts have been failing for 3 days.",
            instructions: "Does this convey urgency?",
            criteriaTrue: "Explicitly time-sensitive",
            criteriaFalse: "No urgency expressed");

        Assert.True(result.Success);
        Assert.Equal("", result.ErrorCode);
        Assert.Equal("", result.ErrorMessage);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("jev-1.13.0", result.Model);
        Assert.Equal(0.95, result.Noul, 9);
        Assert.Equal(307, result.InputTokens);
        Assert.Equal(20, result.OutputTokens);

        var (method, url, body) = Assert.Single(handler.Requests);
        Assert.Equal("POST", method);
        Assert.Equal("https://api.typesafe.ai/v1/systemone", url);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.Equal("jev-latest", root.GetProperty("model").GetString());
        Assert.Contains("payouts", root.GetProperty("state").GetString());
        var q = root.GetProperty("questions").GetProperty("result");
        Assert.Equal("noul", q.GetProperty("type").GetString());
        Assert.Equal("Does this convey urgency?", q.GetProperty("instructions").GetString());
        Assert.Equal("Explicitly time-sensitive", q.GetProperty("criteria").GetProperty("true").GetString());
        Assert.Equal("No urgency expressed", q.GetProperty("criteria").GetProperty("false").GetString());
    }

    [Fact]
    public void EvaluateNoul_WithoutCriteria_OmitsCriteriaKey()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK,
            """{"model":"jev-1.13.0","answers":{"result":{"type":"noul","noul":0.1}},"usage":{"input_tokens":10,"output_tokens":5}}""");
        var client = MakeClient(handler);

        client.EvaluateNoul("sk-test", "some state", "Is this spam?");

        var (_, _, body) = handler.Requests[0];
        using var doc = JsonDocument.Parse(body);
        var q = doc.RootElement.GetProperty("questions").GetProperty("result");
        Assert.False(q.TryGetProperty("criteria", out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void EvaluateNoul_RequiresApiKey(string? apiKey)
    {
        var handler = new FakeHttpMessageHandler();
        var client = MakeClient(handler);

        var result = client.EvaluateNoul(apiKey!, "s", "q?");

        Assert.False(result.Success);
        Assert.Equal("Validation", result.ErrorCode);
        Assert.Equal(0, result.StatusCode);
        Assert.Contains("apiKey is required", result.ErrorMessage);
        Assert.Equal(0, result.Noul);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void EvaluateChoice_HappyPath_MapsProbabilitiesAndConfidence()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK,
            """{"model":"jev-1.13.0","answers":{"result":{"type":"choice","choice":"billing","probabilities":{"billing":0.88,"technical":0.12,"sales":0.0},"confidence":0.81}},"usage":{"input_tokens":318,"output_tokens":34}}""");
        var client = MakeClient(handler);

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

        Assert.True(result.Success);
        Assert.Equal("", result.ErrorCode);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("billing", result.Choice);
        Assert.Equal(0.81, result.Confidence, 9);
        Assert.Equal(3, result.Probabilities.Count);
        Assert.Equal(0.88, result.Probabilities.Single(p => p.Option == "billing").Probability, 9);

        var (_, _, body) = handler.Requests[0];
        using var doc = JsonDocument.Parse(body);
        var criteria = doc.RootElement.GetProperty("questions").GetProperty("result").GetProperty("criteria");
        Assert.Equal("Payments, invoicing, refunds", criteria.GetProperty("billing").GetString());
    }

    [Fact]
    public void EvaluateChoice_EmptyOptions_FailsWithoutCallingApi()
    {
        var handler = new FakeHttpMessageHandler();
        var client = MakeClient(handler);

        var result = client.EvaluateChoice("sk-test", "state", "question?", new List<JevChoiceCriterion>());

        Assert.False(result.Success);
        Assert.Equal("Validation", result.ErrorCode);
        Assert.Equal("", result.Choice);
        Assert.Empty(result.Probabilities);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void EvaluateChoice_TooManyOptions_FailsWithoutCallingApi()
    {
        var handler = new FakeHttpMessageHandler();
        var client = MakeClient(handler);
        var options = Enumerable.Range(0, 256).Select(i => new JevChoiceCriterion { Option = $"opt{i}" }).ToList();

        var result = client.EvaluateChoice("sk-test", "state", "question?", options);

        Assert.False(result.Success);
        Assert.Equal("Validation", result.ErrorCode);
        Assert.Contains("255", result.ErrorMessage);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void EvaluateChoice_EmptyDescription_SerializesAsJsonNull()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK,
            """{"model":"jev-1.13.0","answers":{"result":{"type":"choice","choice":"a","probabilities":{"a":1.0},"confidence":1.0}},"usage":{"input_tokens":1,"output_tokens":1}}""");
        var client = MakeClient(handler);

        client.EvaluateChoice("sk-test", "state", "question?", new List<JevChoiceCriterion> { new() { Option = "a", Description = "" } });

        var (_, _, body) = handler.Requests[0];
        using var doc = JsonDocument.Parse(body);
        var criteria = doc.RootElement.GetProperty("questions").GetProperty("result").GetProperty("criteria");
        Assert.Equal(JsonValueKind.Null, criteria.GetProperty("a").ValueKind);
    }

    [Fact]
    public void EvaluateScore_HappyPath_MapsLegendProbabilitiesAndScore()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK,
            """{"model":"jev-1.13.0","answers":{"result":{"type":"score","score":1.05,"legend":{"0":"Calm","1":"Frustrated","2":"Very angry"},"probabilities":{"0":0.0,"1":0.95,"2":0.05},"confidence":0.92}},"usage":{"input_tokens":304,"output_tokens":18}}""");
        var client = MakeClient(handler);

        var result = client.EvaluateScore(
            apiKey: "sk-test",
            state: "Help! My payouts have been failing for 3 days.",
            instructions: "How frustrated is the customer?",
            levels: new List<string> { "Calm", "Frustrated", "Very angry" });

        Assert.True(result.Success);
        Assert.Equal("", result.ErrorCode);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal(1.05, result.Score, 9);
        Assert.Equal(0.92, result.Confidence, 9);
        Assert.Equal(3, result.Levels.Count);
        Assert.Equal("Calm", result.Levels[0].Description);
        Assert.Equal(0.95, result.Levels[1].Probability, 9);
        Assert.True(result.Levels[0].Level < result.Levels[1].Level && result.Levels[1].Level < result.Levels[2].Level);
    }

    [Fact]
    public void EvaluateScore_FewerThanTwoLevels_Fails()
    {
        var handler = new FakeHttpMessageHandler();
        var client = MakeClient(handler);

        var result = client.EvaluateScore("sk-test", "s", "q?", new List<string> { "only-one" });

        Assert.False(result.Success);
        Assert.Equal("Validation", result.ErrorCode);
        Assert.Equal(0, result.Score);
        Assert.Empty(result.Levels);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void EvaluateScore_MoreThanTenLevels_Fails()
    {
        var handler = new FakeHttpMessageHandler();
        var client = MakeClient(handler);
        var levels = Enumerable.Range(0, 11).Select(i => $"L{i}").ToList();

        var result = client.EvaluateScore("sk-test", "s", "q?", levels);

        Assert.False(result.Success);
        Assert.Equal("Validation", result.ErrorCode);
        Assert.Contains("10 levels", result.ErrorMessage);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void EvaluateRaw_BatchesMultipleQuestions_AndRoundTripsAnswersJson()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK,
            """{"model":"jev-1.13.0","answers":{"is_lead":{"type":"noul","noul":0.91},"urgency":{"type":"score","score":1.4,"legend":{"0":"Low","1":"Medium","2":"High"},"probabilities":{"0":0.1,"1":0.4,"2":0.5},"confidence":0.6}},"usage":{"input_tokens":500,"output_tokens":40}}""");
        var client = MakeClient(handler);

        const string questionsJson = """
        {
          "is_lead": {"type":"noul","instructions":"Is this a sales lead?"},
          "urgency": {"type":"score","instructions":"How urgent?","criteria":["Low","Medium","High"]}
        }
        """;

        var result = client.EvaluateRaw("sk-test", "Customer emailed about pricing.", questionsJson);

        Assert.True(result.Success);
        Assert.Equal("", result.ErrorCode);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("jev-1.13.0", result.Model);
        Assert.Equal(500, result.InputTokens);
        Assert.Equal(40, result.OutputTokens);

        using var answers = JsonDocument.Parse(result.AnswersJson);
        Assert.Equal(0.91, answers.RootElement.GetProperty("is_lead").GetProperty("noul").GetDouble(), 9);
        Assert.Equal(1.4, answers.RootElement.GetProperty("urgency").GetProperty("score").GetDouble(), 9);

        var (_, _, body) = handler.Requests[0];
        using var reqDoc = JsonDocument.Parse(body);
        Assert.Equal("noul", reqDoc.RootElement.GetProperty("questions").GetProperty("is_lead").GetProperty("type").GetString());
    }

    [Fact]
    public void EvaluateRaw_JsonState_IsSentStructuredNotAsQuotedString()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK,
            """{"model":"jev-1.13.0","answers":{"q":{"type":"noul","noul":0.5}},"usage":{"input_tokens":1,"output_tokens":1}}""");
        var client = MakeClient(handler);

        client.EvaluateRaw("sk-test", """{"messages":[{"role":"user","content":"hi"}]}""", """{"q":{"type":"noul","instructions":"x"}}""");

        var (_, _, body) = handler.Requests[0];
        using var doc = JsonDocument.Parse(body);
        var state = doc.RootElement.GetProperty("state");
        Assert.Equal(JsonValueKind.Object, state.ValueKind);
        Assert.Equal("user", state.GetProperty("messages")[0].GetProperty("role").GetString());
    }

    [Fact]
    public void EvaluateRaw_PlainTextState_StaysAJsonString()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK,
            """{"model":"jev-1.13.0","answers":{"q":{"type":"noul","noul":0.5}},"usage":{"input_tokens":1,"output_tokens":1}}""");
        var client = MakeClient(handler);

        client.EvaluateRaw("sk-test", "just plain text, not json", """{"q":{"type":"noul","instructions":"x"}}""");

        var (_, _, body) = handler.Requests[0];
        using var doc = JsonDocument.Parse(body);
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("state").ValueKind);
    }

    [Fact]
    public void EvaluateRaw_InvalidQuestionsJson_FailsWithoutCallingApi()
    {
        var handler = new FakeHttpMessageHandler();
        var client = MakeClient(handler);

        var result = client.EvaluateRaw("sk-test", "state", "{not valid json");

        Assert.False(result.Success);
        Assert.Equal("Validation", result.ErrorCode);
        Assert.Contains("not valid JSON", result.ErrorMessage);
        Assert.Equal("", result.AnswersJson);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void Errors_401_FailsImmediatelyWithoutRetry()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.Unauthorized, """{"message":"Invalid API key"}""");
        var client = MakeClient(handler);

        var result = client.EvaluateNoul("bad-key", "s", "q?");

        Assert.False(result.Success);
        Assert.Equal("Unauthorized", result.ErrorCode);
        Assert.Equal(401, result.StatusCode);
        Assert.Contains("Invalid API key", result.ErrorMessage);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public void Errors_422_FailsImmediatelyWithoutRetry()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.UnprocessableEntity, """{"message":"criteria must have at least 2 levels"}""");
        var client = MakeClient(handler);

        var result = client.EvaluateScore("sk-test", "s", "q?", new List<string> { "a", "b" });

        Assert.False(result.Success);
        Assert.Equal("InvalidRequest", result.ErrorCode);
        Assert.Equal(422, result.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public void Errors_UnmappedStatus_ReportsHttpError()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.InternalServerError, """{"message":"boom"}""");
        var client = MakeClient(handler);

        var result = client.EvaluateNoul("sk-test", "s", "q?");

        Assert.False(result.Success);
        Assert.Equal("HttpError", result.ErrorCode);
        Assert.Equal(500, result.StatusCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public void Errors_ResponseMissingTheAnswer_ReportsBadResponse()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK,
            """{"model":"jev-1.13.0","answers":{"something_else":{"type":"noul","noul":0.5}},"usage":{"input_tokens":1,"output_tokens":1}}""");
        var client = MakeClient(handler);

        var result = client.EvaluateNoul("sk-test", "s", "q?");

        Assert.False(result.Success);
        Assert.Equal("BadResponse", result.ErrorCode);
        Assert.Equal(0, result.Noul);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public void Errors_ResponseMissingAField_ReportsBadResponse()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.OK,
            """{"model":"jev-1.13.0","answers":{"result":{"type":"noul"}},"usage":{"input_tokens":1,"output_tokens":1}}""");
        var client = MakeClient(handler);

        var result = client.EvaluateNoul("sk-test", "s", "q?");

        Assert.False(result.Success);
        Assert.Equal("BadResponse", result.ErrorCode);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public void Retries_429ThenSuccess_SucceedsOnThirdAttempt()
    {
        var handler = new FakeHttpMessageHandler()
            .Enqueue((HttpStatusCode)429, """{"message":"rate limited"}""", TimeSpan.FromMilliseconds(5))
            .Enqueue((HttpStatusCode)429, """{"message":"rate limited"}""", TimeSpan.FromMilliseconds(5))
            .Enqueue(HttpStatusCode.OK, """{"model":"jev-1.13.0","answers":{"result":{"type":"noul","noul":0.7}},"usage":{"input_tokens":1,"output_tokens":1}}""");
        var client = MakeClient(handler);

        var result = client.EvaluateNoul("sk-test", "s", "q?");

        Assert.True(result.Success);
        Assert.Equal(0.7, result.Noul, 9);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public void Retries_529Exhausted_FailsAfterMaxAttempts()
    {
        var handler = new FakeHttpMessageHandler();
        for (var i = 0; i < 4; i++)
        {
            handler.Enqueue((HttpStatusCode)529, """{"message":"overloaded"}""", TimeSpan.FromMilliseconds(5));
        }
        var client = MakeClient(handler);

        var result = client.EvaluateNoul("sk-test", "s", "q?");

        Assert.False(result.Success);
        Assert.Equal("Overloaded", result.ErrorCode);
        Assert.Equal(529, result.StatusCode);
        Assert.Equal(4, handler.Requests.Count);
    }

    [Fact]
    public void Retries_429Exhausted_ReportsRateLimit()
    {
        var handler = new FakeHttpMessageHandler();
        for (var i = 0; i < 4; i++)
        {
            handler.Enqueue((HttpStatusCode)429, """{"message":"rate limited"}""", TimeSpan.FromMilliseconds(5));
        }
        var client = MakeClient(handler);

        var result = client.EvaluateNoul("sk-test", "s", "q?");

        Assert.False(result.Success);
        Assert.Equal("RateLimit", result.ErrorCode);
        Assert.Equal(429, result.StatusCode);
        Assert.Equal(4, handler.Requests.Count);
    }

    [Fact]
    public void Transport_FailureExhausted_ReportsNetwork()
    {
        // Transport failures back off on the connector's own schedule (no Retry-After to
        // honor), so this test spends ~2.8s in Task.Delay covering all four attempts.
        var handler = new FakeHttpMessageHandler();
        for (var i = 0; i < 4; i++)
        {
            handler.EnqueueThrow(new HttpRequestException("no such host"));
        }
        var client = MakeClient(handler);

        var result = client.EvaluateNoul("sk-test", "s", "q?");

        Assert.False(result.Success);
        Assert.Equal("Network", result.ErrorCode);
        Assert.Equal(0, result.StatusCode);
        Assert.Contains("no such host", result.ErrorMessage);
        Assert.Equal(4, handler.Requests.Count);
    }

    [Fact]
    public void Failures_NeverEchoTheApiKey()
    {
        var handler = new FakeHttpMessageHandler().Enqueue(HttpStatusCode.Unauthorized, """{"message":"Invalid API key"}""");
        var client = MakeClient(handler);

        var result = client.EvaluateNoul("sk-super-secret-key", "s", "q?");

        Assert.False(result.Success);
        Assert.DoesNotContain("sk-super-secret-key", result.ErrorMessage);
    }
}
