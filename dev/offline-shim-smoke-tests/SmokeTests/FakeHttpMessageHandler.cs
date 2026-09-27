using System.Net;
using System.Net.Http;
using System.Text;

namespace JevConnector.ShimSmokeTests;

/// <summary>
/// Hand-rolled fake HTTP handler (no Moq/RichardSzalay needed) that returns
/// queued responses in order and records every request it saw.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Status, string Body, TimeSpan? RetryAfter)> _responses = new();
    public List<(string Method, string Url, string Body)> Requests { get; } = new();

    public FakeHttpMessageHandler Enqueue(HttpStatusCode status, string body, TimeSpan? retryAfter = null)
    {
        _responses.Enqueue((status, body, retryAfter));
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

        var (status, respBody, retryAfter) = _responses.Dequeue();
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
