using Jev.Net;

namespace PinkRooster.ProviderConfig.Jev;

/// <summary>The default <see cref="ISystemOneClientFactory"/>: resolves clients through the provider registry.</summary>
/// <param name="factory">The provider client factory.</param>
internal sealed class SystemOneClientFactory(IProviderClientFactory factory) : ISystemOneClientFactory
{
    /// <inheritdoc />
    public ITypeSafeClient GetSystemOneClient(string providerId, string modelId) => factory.GetClient<ITypeSafeClient>(providerId, modelId);
}
