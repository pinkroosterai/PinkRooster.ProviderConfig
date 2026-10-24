using Jev.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PinkRooster.ProviderConfig.Jev;

/// <summary>Registers the Jev provider type.</summary>
public static class JevProviderConfigBuilderExtensions
{
    extension(ProviderConfigBuilder builder)
    {
        /// <summary>Registers the <c>jev</c> provider type and <see cref="ISystemOneClientFactory"/>. Safe to call more than once.</summary>
        /// <returns>The builder.</returns>
        public ProviderConfigBuilder AddJev()
        {
            // A static method group, not a lambda: the registry treats a re-registration as a no-op only when the creators are equal.
            builder.AddProviderType<ITypeSafeClient>("jev", JevProviderType.Create, new ProviderTypeOptions
            {
                Validate = RequireKey,
            });
            builder.Services.TryAddSingleton<ISystemOneClientFactory, SystemOneClientFactory>();
            return builder;
        }

        /// <summary>Registers an <see cref="ITypeSafeClient"/> pinned to a provider and model.</summary>
        /// <param name="providerId">The ID of a configured <c>jev</c> provider.</param>
        /// <param name="modelId">The model to pin.</param>
        /// <param name="serviceKey">When set, the client is registered as a keyed service under this key; otherwise it is unkeyed.</param>
        /// <returns>The builder.</returns>
        /// <exception cref="ArgumentException">An argument is empty or blank.</exception>
        public ProviderConfigBuilder AddSystemOneClient(string providerId, string modelId, object? serviceKey = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
            ArgumentException.ThrowIfNullOrWhiteSpace(modelId);
            if (serviceKey is null)
            {
                builder.Services.AddSingleton<ITypeSafeClient>(sp => sp.GetRequiredService<ISystemOneClientFactory>().GetSystemOneClient(providerId, modelId));
            }
            else
            {
                builder.Services.AddKeyedSingleton<ITypeSafeClient>(serviceKey, (sp, _) => sp.GetRequiredService<ISystemOneClientFactory>().GetSystemOneClient(providerId, modelId));
            }

            return builder;
        }
    }

    private static IEnumerable<string> RequireKey(ProviderOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            yield return "ApiKey is required for type 'jev'.";
        }
    }
}
