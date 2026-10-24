using Microsoft.Extensions.DependencyInjection;

namespace PinkRooster.ProviderConfig;

/// <summary>Configures provider support. Returned by <c>AddProviderConfig</c>.</summary>
public sealed class ProviderConfigBuilder
{
    private readonly ProviderTypeRegistry _types;

    internal ProviderConfigBuilder(IServiceCollection services, ProviderTypeRegistry types)
    {
        Services = services;
        _types = types;
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

    /// <summary>
    /// Registers a provider type: a name that <c>Providers:{id}:Type</c> can refer to, and the function that creates the client for one
    /// configured provider. Registering the same name again with the same client kind and the same creator does nothing; any other
    /// registration under a taken name throws.
    /// </summary>
    /// <typeparam name="TClient">The client kind the type creates.</typeparam>
    /// <param name="type">The type name, matched ignoring case.</param>
    /// <param name="create">Creates a model-to-client function for one configured provider.</param>
    /// <param name="options">Optional validation and behaviour of the type.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentException">The name is blank, or taken by a different registration.</exception>
    public ProviderConfigBuilder AddProviderType<TClient>(string type, Func<ProviderContext, Func<string, TClient>> create, ProviderTypeOptions? options = null)
        where TClient : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        ArgumentNullException.ThrowIfNull(create);
        _types.Add(new ProviderTypeRegistration(type, typeof(TClient), create, options ?? new ProviderTypeOptions()));
        return this;
    }
}
