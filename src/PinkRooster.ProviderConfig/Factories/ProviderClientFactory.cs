using System.Collections.Concurrent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace PinkRooster.ProviderConfig;

/// <summary>Creates each provider's client function once, through its registered type.</summary>
internal sealed class ProviderClientFactory(IOptions<ProviderConfigOptions> options, ProviderTypeRegistry types, IServiceProvider services) : IProviderClientFactory, IChatClientFactory, IDisposable
{
    private readonly ConcurrentBag<IDisposable> _owned = [];
    private readonly Lock _gate = new();
    private bool _disposed;

    private readonly ConcurrentDictionary<string, Lazy<Delegate>> _providers = new(StringComparer.OrdinalIgnoreCase);

    public IChatClient GetChatClient(string providerId, string modelId)
    {
        IChatClient client = GetClient<IChatClient>(providerId, modelId);
        if (options.Value.Providers.TryGetValue(providerId, out ProviderOptions? providerOptions)
            && providerOptions.Models.Count > 0)
        {
            return new DefaultOptionsChatClient(client, modelId, (IReadOnlyDictionary<string, ModelOptions>)providerOptions.Models);
        }

        return client;
    }

    public TClient GetClient<TClient>(string providerId, string modelId) where TClient : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        IDictionary<string, ProviderOptions> configured = options.Value.Providers;
        if (!configured.TryGetValue(providerId, out ProviderOptions? providerOptions))
        {
            string known = configured.Count == 0 ? "(none)" : string.Join(", ", configured.Keys.Order(StringComparer.Ordinal));
            throw new InvalidOperationException($"No upstream provider '{providerId}' is configured. Configured providers: {known}.");
        }

        string configuredId = configured.Keys.First(key => string.Equals(key, providerId, StringComparison.OrdinalIgnoreCase));
        if (providerOptions.Type is null || !types.TryGet(providerOptions.Type, out ProviderTypeRegistration? registration))
        {
            throw new InvalidOperationException($"Upstream provider '{configuredId}' has no registered type.");
        }

        // Check the kind before anything is created, so a wrong request never runs the type's creator.
        if (typeof(TClient) != registration.ClientType)
        {
            throw new InvalidOperationException(
                $"Upstream provider '{configuredId}' has type '{registration.Name}', which creates {registration.ClientType.Name}, not {typeof(TClient).Name}.");
        }

        Lazy<Delegate> creator = _providers.GetOrAdd(
            configuredId,
            id => new Lazy<Delegate>(() => Create<TClient>(id, providerOptions, registration), LazyThreadSafetyMode.ExecutionAndPublication));
        return ((Func<string, TClient>)creator.Value)(modelId);
    }

    private Func<string, TClient> Create<TClient>(string providerId, ProviderOptions providerOptions, ProviderTypeRegistration registration)
        where TClient : class =>
        ((Func<ProviderContext, Func<string, TClient>>)registration.Create)(new ProviderContext(providerId, providerOptions, services, Own));

    private void Own(IDisposable disposable)
    {
        lock (_gate)
        {
            if (!_disposed)
            {
                _owned.Add(disposable);
                return;
            }
        }

        // The factory is already disposed: nothing will dispose this later, so do it now.
        disposable.Dispose();
    }

    /// <summary>Disposes every object the provider types registered, once; later calls do nothing.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        while (_owned.TryTake(out IDisposable? disposable))
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception)
            {
                // Throwing here would stop the container disposing the singletons after this one, so every client is disposed and a failure is dropped.
            }
        }
    }
}
