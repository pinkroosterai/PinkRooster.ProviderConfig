using Microsoft.Extensions.AI;

namespace PinkRooster.ProviderConfig.OpenAI;

/// <summary>Registers the OpenAI provider types.</summary>
public static class OpenAIProviderConfigBuilderExtensions
{
    extension(ProviderConfigBuilder builder)
    {
        /// <summary>
        /// Registers the <c>openai</c> (Chat Completions) and <c>openai_responses</c> (Responses, honours <c>Store</c>) provider types.
        /// Safe to call more than once.
        /// </summary>
        /// <returns>The builder.</returns>
        public ProviderConfigBuilder AddOpenAI()
        {
            // Static method groups, not lambdas: the registry treats a re-registration as a no-op only when the creators are equal.
            builder.AddProviderType<IChatClient>("openai", OpenAIProviderType.CreateChatCompletions, new ProviderTypeOptions
            {
                Validate = options => RequireKey("openai", options),
            });
            builder.AddProviderType<IChatClient>("openai_responses", OpenAIProviderType.CreateResponses, new ProviderTypeOptions
            {
                Validate = options => RequireKey("openai_responses", options),
                UsesStore = true,
            });
            return builder;
        }
    }

    private static IEnumerable<string> RequireKey(string type, ProviderOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey) && options.Endpoint is null)
        {
            yield return $"ApiKey is required for type '{type}' without an Endpoint.";
        }
    }
}
