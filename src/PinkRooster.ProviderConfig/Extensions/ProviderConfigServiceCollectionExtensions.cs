using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace PinkRooster.ProviderConfig;

/// <summary>Service registration for provider configuration.</summary>
public static class ProviderConfigServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the provider options and binds them from the <c>Providers</c> configuration section, when an
        /// <c>IConfiguration</c> is registered. Safe to call more than once.
        /// </summary>
        /// <returns>A builder to configure provider support further.</returns>
        public ProviderConfigBuilder AddProviderConfig()
        {
            services.AddOptions<ProviderConfigOptions>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<ProviderConfigOptions>, ConfigurationBinding>());
            return new ProviderConfigBuilder(services);
        }
    }
}
