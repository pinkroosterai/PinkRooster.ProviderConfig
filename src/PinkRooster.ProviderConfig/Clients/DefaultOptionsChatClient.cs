using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace PinkRooster.ProviderConfig;

/// <summary>Decorates an <see cref="IChatClient"/> with per-model default chat options.</summary>
internal sealed class DefaultOptionsChatClient(IChatClient inner, string defaultModelId, IReadOnlyDictionary<string, ModelOptions> models)
    : DelegatingChatClient(inner)
{
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return base.GetResponseAsync(messages, ResolveOptions(options), cancellationToken);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (ChatResponseUpdate update in base.GetStreamingResponseAsync(messages, ResolveOptions(options), cancellationToken).ConfigureAwait(false))
        {
            yield return update;
        }
    }

    private ChatOptions? ResolveOptions(ChatOptions? options)
    {
        string effectiveModelId = options?.ModelId ?? defaultModelId;
        if (!models.TryGetValue(effectiveModelId, out ModelOptions? modelDefaults))
        {
            return options;
        }

        ChatOptions result = options?.Clone() ?? new ChatOptions();

        result.Temperature ??= modelDefaults.Temperature;
        result.MaxOutputTokens ??= modelDefaults.MaxOutputTokens;
        result.TopP ??= modelDefaults.TopP;
        result.TopK ??= modelDefaults.TopK;
        result.FrequencyPenalty ??= modelDefaults.FrequencyPenalty;
        result.PresencePenalty ??= modelDefaults.PresencePenalty;
        result.Seed ??= modelDefaults.Seed;

        if (modelDefaults.ReasoningEffort is { } effort)
        {
            if (result.Reasoning is null)
            {
                result.Reasoning = new ReasoningOptions { Effort = effort };
            }
            else
            {
                result.Reasoning.Effort ??= effort;
            }
        }

        if (modelDefaults.StopSequences is { Count: > 0 } && result.StopSequences is null)
        {
            result.StopSequences = [.. modelDefaults.StopSequences];
        }

        if (modelDefaults.AdditionalProperties is { Count: > 0 })
        {
            if (result.AdditionalProperties is null)
            {
                result.AdditionalProperties = new AdditionalPropertiesDictionary(modelDefaults.AdditionalProperties);
            }
            else
            {
                foreach ((string key, object? value) in modelDefaults.AdditionalProperties)
                {
                    result.AdditionalProperties.TryAdd(key, value);
                }
            }
        }

        return result;
    }
}
