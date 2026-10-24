using Microsoft.Extensions.AI;

namespace PinkRooster.ProviderConfig;

/// <summary>Gives handlers a chat client for a configured upstream provider.</summary>
public interface IChatClientFactory
{
    /// <summary>
    /// Gets a chat client for a model of a configured provider. <paramref name="modelId"/> is the default model; a caller's
    /// <c>ChatOptions.ModelId</c> overrides it. The caller need not dispose the client; disposing it is harmless.
    /// </summary>
    /// <param name="providerId">The provider ID under <c>Providers</c>, matched ignoring case.</param>
    /// <param name="modelId">The default model for the client.</param>
    /// <returns>The chat client.</returns>
    /// <exception cref="ArgumentException">An argument is empty or blank.</exception>
    /// <exception cref="InvalidOperationException">No provider is configured under the ID, or its type does not create chat clients.</exception>
    IChatClient GetChatClient(string providerId, string modelId);
}
