using Microsoft.Extensions.AI;

namespace PinkRooster.ProviderConfig.Tests.TestSupport;

/// <summary>An embedding generator that only records which provider and model it was made for.</summary>
internal sealed class FakeEmbeddingGenerator(string providerId, string modelId, int? dimensions = null) : IEmbeddingGenerator<string, Embedding<float>>
{
    public string ProviderId { get; } = providerId;

    public string ModelId { get; } = modelId;

    public int? Dimensions { get; } = dimensions;

    public EmbeddingGeneratorMetadata Metadata { get; } = new("fake", null, modelId, dimensions);

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
