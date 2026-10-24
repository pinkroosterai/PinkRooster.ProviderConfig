using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace PinkRooster.ProviderConfig.Tests.TestSupport;

/// <summary>A fake upstream HTTP service: records each request and answers from a queue.</summary>
internal sealed class StubUpstream : HttpMessageHandler
{
    private readonly object _gate = new();
    private readonly List<RecordedRequest> _requests = [];
    private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> _responses = new();

    /// <summary>Set when a hanging request's token cancels.</summary>
    public TaskCompletionSource<bool> Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The requests received so far.</summary>
    public IReadOnlyList<RecordedRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>Queues a JSON answer.</summary>
    public StubUpstream RespondJson(string json) => Enqueue((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    }));

    /// <summary>Queues a server-sent events answer, one <c>data:</c> line per argument.</summary>
    public StubUpstream RespondSse(params string[] dataLines) => Enqueue((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(string.Concat(dataLines.Select(line => $"data: {line}\n\n")), Encoding.UTF8, "text/event-stream"),
    }));

    /// <summary>Queues an answer that never comes: it waits until the request's token cancels.</summary>
    public StubUpstream Hang() => Enqueue(async (_, token) =>
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
        }
        catch (OperationCanceledException)
        {
            Cancelled.TrySetResult(true);
            throw;
        }

        throw new InvalidOperationException("Unreachable.");
    });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
        {
            headers[header.Key] = string.Join(",", header.Value);
        }

        JsonObject? body = null;
        if (request.Content is not null)
        {
            string text = await request.Content.ReadAsStringAsync(cancellationToken);
            body = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text) as JsonObject;
        }

        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond;
        lock (_gate)
        {
            _requests.Add(new RecordedRequest(request.Method, request.RequestUri!, headers, body));
            respond = _responses.Count > 0 ? _responses.Dequeue() : throw new InvalidOperationException("The stub has no queued response.");
        }

        return await respond(request, cancellationToken);
    }

    private StubUpstream Enqueue(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        lock (_gate)
        {
            _responses.Enqueue(respond);
        }

        return this;
    }

    internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, IReadOnlyDictionary<string, string> Headers, JsonObject? Body);
}
