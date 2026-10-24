using Jev.Net;

namespace PinkRooster.ProviderConfig.Jev;

/// <summary>Creates Jev System One clients for configured <c>jev</c> providers.</summary>
public interface ISystemOneClientFactory
{
    /// <summary>Gets a client for a provider with the model pinned: requests that name no model use <paramref name="modelId"/>.</summary>
    /// <param name="providerId">The ID of a configured provider of type <c>jev</c>.</param>
    /// <param name="modelId">The model to send when the caller names none.</param>
    /// <returns>The System One client. It makes no request until it is used.</returns>
    /// <exception cref="ArgumentException">An argument is empty or blank.</exception>
    /// <exception cref="InvalidOperationException">No provider is configured under the ID, or its type does not create System One clients.</exception>
    ITypeSafeClient GetSystemOneClient(string providerId, string modelId);
}
