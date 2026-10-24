using Microsoft.Extensions.DependencyInjection;

namespace PinkRooster.ProviderConfig;

/// <summary>Configures provider support. Returned by <c>AddProviderConfig</c>.</summary>
public sealed class ProviderConfigBuilder
{
    internal ProviderConfigBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>The service collection being configured.</summary>
    public IServiceCollection Services { get; }

    /// <summary>Configures the provider options in code. Runs after the configuration binding, so code wins for the same id.</summary>
    /// <param name="configure">The configuration action.</param>
    /// <returns>This builder.</returns>
    public ProviderConfigBuilder Configure(Action<ProviderConfigOptions> configure)
    {
        Services.Configure(configure);
        return this;
    }
}
