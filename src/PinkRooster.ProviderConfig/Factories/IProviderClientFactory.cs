namespace PinkRooster.ProviderConfig;

/// <summary>Gives code a client of a given kind for a configured upstream provider.</summary>
public interface IProviderClientFactory
{
    /// <summary>
    /// Gets a client for a model of a configured provider. <paramref name="modelId"/> is the default model. The caller need not dispose
    /// the client; disposing it is harmless.
    /// </summary>
    /// <typeparam name="TClient">The client kind the provider's type must create.</typeparam>
    /// <param name="providerId">The provider ID under <c>Providers</c>, matched ignoring case.</param>
    /// <param name="modelId">The default model for the client.</param>
    /// <returns>The client.</returns>
    /// <exception cref="ArgumentException">An argument is empty or blank.</exception>
    /// <exception cref="InvalidOperationException">No provider is configured under the ID, or its type creates another client kind.</exception>
    TClient GetClient<TClient>(string providerId, string modelId) where TClient : class;
}
