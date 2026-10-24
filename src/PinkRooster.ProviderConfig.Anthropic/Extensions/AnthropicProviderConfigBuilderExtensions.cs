using Microsoft.Extensions.AI;

namespace PinkRooster.ProviderConfig.Anthropic;

/// <summary>Registers the Anthropic provider type.</summary>
public static class AnthropicProviderConfigBuilderExtensions
{
    extension(ProviderConfigBuilder builder)
    {
        /// <summary>Registers the <c>anthropic</c> provider type. Safe to call more than once.</summary>
        /// <returns>The builder.</returns>
        public ProviderConfigBuilder AddAnthropic()
        {
            // A static method group, not a lambda: the registry treats a re-registration as a no-op only when the creators are equal.
            builder.AddProviderType<IChatClient>("anthropic", AnthropicProviderType.Create, new ProviderTypeOptions
            {
                Validate = RequireKey,
            });
            return builder;
        }
    }

    private static IEnumerable<string> RequireKey(ProviderOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            yield return "ApiKey is required for type 'anthropic'.";
        }
    }
}
