using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace PinkRooster.ProviderConfig.Tests.TestSupport;

internal sealed class RecordingChatClient : IChatClient
{
    public List<ChatMessage> LastMessages { get; private set; } = [];
    public ChatOptions? LastOptions { get; private set; }
    public int InvocationCount { get; private set; }

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        InvocationCount++;
        LastMessages = [.. messages];
        LastOptions = options;
        return Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, "test response")]));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        InvocationCount++;
        LastMessages = [.. messages];
        LastOptions = options;
        yield return new ChatResponseUpdate(ChatRole.Assistant, "streaming update");
        await Task.CompletedTask;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;
    public void Dispose() { }
}
