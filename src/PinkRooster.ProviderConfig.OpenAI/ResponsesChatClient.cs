// OPENAI001: CreateResponseOptions, ResponseResult and the streaming update types of the OpenAI SDK are experimental.
#pragma warning disable OPENAI001

using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;
using OpenAI.Responses;

namespace PinkRooster.ProviderConfig.OpenAI;

/// <summary>
/// Wraps the Responses API chat client of an <c>openai_responses</c> provider: it sends the provider's <c>store</c> setting unless the
/// caller set one, and turns a failure the upstream reports inside a successful HTTP response into an exception.
/// </summary>
internal sealed class ResponsesChatClient(IChatClient inner, bool store) : DelegatingChatClient(inner)
{
    /// <inheritdoc />
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ChatResponse response = await base.GetResponseAsync(messages, WithStore(options), cancellationToken).ConfigureAwait(false);
        if (response.RawRepresentation is ResponseResult result && (result.Error is not null || result.Status == ResponseStatus.Failed))
        {
            throw Failure(result.Error?.Code.ToString(), result.Error?.Message);
        }

        return response;
    }

    /// <inheritdoc />
    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (ChatResponseUpdate update in base.GetStreamingResponseAsync(messages, WithStore(options), cancellationToken).ConfigureAwait(false))
        {
            switch (update.RawRepresentation)
            {
                case StreamingResponseErrorUpdate error:
                    throw Failure(error.Code, error.Message);
                case StreamingResponseFailedUpdate failed:
                    throw Failure(failed.Response?.Error?.Code.ToString(), failed.Response?.Error?.Message);
            }

            yield return update;
        }
    }

    private static InvalidOperationException Failure(string? code, string? message)
    {
        string shownCode = string.IsNullOrWhiteSpace(code) ? "unknown" : code;
        string shownMessage = string.IsNullOrWhiteSpace(message) ? "no message" : message;
        return new InvalidOperationException($"The upstream provider reported a failure: {shownCode}: {shownMessage}");
    }

    /// <summary>Clones the caller's options, so theirs stay untouched, with a factory that keeps their <c>store</c> or sends the provider's.</summary>
    private ChatOptions WithStore(ChatOptions? options)
    {
        ChatOptions sent = options?.Clone() ?? new ChatOptions();
        Func<IChatClient, object?>? callers = sent.RawRepresentationFactory;
        sent.RawRepresentationFactory = client =>
        {
            CreateResponseOptions created = callers?.Invoke(client) as CreateResponseOptions ?? new CreateResponseOptions();
            created.StoredOutputEnabled ??= store;
            return created;
        };
        return sent;
    }
}
