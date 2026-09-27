using System.Net;
using System.Text;

namespace VRG.Jev.Tests;

/// <summary>Fake HTTP handler that returns queued responses in order and records every request.</summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Status, string Body, TimeSpan? RetryAfter, Exception? Throw)> _responses = new();
    public List<(string Method, string Url, string Body)> Requests { get; } = new();

    public FakeHttpMessageHandler Enqueue(HttpStatusCode status, string body, TimeSpan? retryAfter = null)
    {
        _responses.Enqueue((status, body, retryAfter, null));
        return this;
    }

    /// <summary>Queues a transport-level failure (no response), the way a DNS/TLS/timeout error behaves.</summary>
    public FakeHttpMessageHandler EnqueueThrow(Exception exception)
    {
        _responses.Enqueue((default, "", null, exception));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request.Method.Method, request.RequestUri!.ToString(), body));

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException("FakeHttpMessageHandler: no more queued responses.");
        }

        var (status, respBody, retryAfter, toThrow) = _responses.Dequeue();
        if (toThrow is not null)
        {
            throw toThrow;
        }

        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(respBody, Encoding.UTF8, "application/json"),
        };
        if (retryAfter.HasValue)
        {
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(retryAfter.Value);
        }
        return response;
    }
}
