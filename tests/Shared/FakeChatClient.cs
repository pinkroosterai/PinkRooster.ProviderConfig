using Microsoft.Extensions.AI;

namespace PinkRooster.ProviderConfig.Tests.TestSupport;

/// <summary>A chat client that only records which provider and model it was made for.</summary>
internal sealed class FakeChatClient(string providerId, string modelId) : IChatClient
{
    public string ProviderId { get; } = providerId;

    public string ModelId { get; } = modelId;

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
