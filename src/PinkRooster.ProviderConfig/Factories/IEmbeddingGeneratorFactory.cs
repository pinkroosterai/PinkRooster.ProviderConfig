using Microsoft.Extensions.AI;

namespace PinkRooster.ProviderConfig;

/// <summary>Gives handlers an embedding generator for a configured upstream provider.</summary>
public interface IEmbeddingGeneratorFactory
{
    /// <summary>
    /// Gets an embedding generator for a model of a configured provider. <paramref name="modelId"/> is the default model.
    /// The caller need not dispose the client; disposing it is harmless.
    /// </summary>
    /// <param name="providerId">The provider ID under <c>Providers</c>, matched ignoring case.</param>
    /// <param name="modelId">The default model for the generator.</param>
    /// <returns>The embedding generator.</returns>
    /// <exception cref="ArgumentException">An argument is empty or blank.</exception>
    /// <exception cref="InvalidOperationException">No provider is configured under the ID, or its type does not create embedding generators.</exception>
    IEmbeddingGenerator<string, Embedding<float>> GetEmbeddingGenerator(string providerId, string modelId);
}
