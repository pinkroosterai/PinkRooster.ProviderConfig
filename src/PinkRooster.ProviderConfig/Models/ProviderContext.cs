namespace PinkRooster.ProviderConfig;

/// <summary>What a provider type gets when it creates the client for one configured provider.</summary>
public sealed class ProviderContext
{
    private readonly Action<IDisposable> _own;

    internal ProviderContext(string providerId, ProviderOptions options, IServiceProvider services, Action<IDisposable> own)
    {
        _own = own;
        ProviderId = providerId;
        Options = options;
        Services = services;
    }

    /// <summary>The provider ID, as configured under <c>Providers</c>.</summary>
    public string ProviderId { get; }

    /// <summary>The settings of the provider.</summary>
    public ProviderOptions Options { get; }

    /// <summary>The application services.</summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Hands an object over to the client factory, which disposes it once when the container shuts down. A provider type that owns an
    /// SDK client or an <see cref="HttpClient"/> registers it here; the per-model chat clients it returns are not disposed.
    /// </summary>
    /// <param name="disposable">The object to dispose with the factory.</param>
    public void RegisterDisposable(IDisposable disposable)
    {
        ArgumentNullException.ThrowIfNull(disposable);
        _own(disposable);
    }
}
