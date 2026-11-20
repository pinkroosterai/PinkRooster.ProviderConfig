using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PinkRooster.ProviderConfig.Tests.TestSupport;

namespace PinkRooster.ProviderConfig.Tests.Builders;

public sealed class AddEmbeddingGeneratorTests
{
    private int _creations;

    private ProviderConfigBuilder NewBuilder(ServiceCollection services) =>
        services.AddProviderConfig()
            .AddProviderType<IEmbeddingGenerator<string, Embedding<float>>>("fake_embed", context =>
            {
                Interlocked.Increment(ref _creations);
                return model => new FakeEmbeddingGenerator(context.ProviderId, model);
            })
            .Configure(options => options.Providers["up"] = new ProviderOptions { Type = "fake_embed" });

    [Fact]
    public void AddEmbeddingGenerator_RegistersAnUnkeyedGenerator_ForTheModel()
    {
        ServiceCollection services = new();
        NewBuilder(services).AddEmbeddingGenerator("up", "text-embed-3");

        FakeEmbeddingGenerator fake = (FakeEmbeddingGenerator)services.BuildServiceProvider()
            .GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        Assert.Equal("up", fake.ProviderId);
        Assert.Equal("text-embed-3", fake.ModelId);
    }

    [Fact]
    public void AddEmbeddingGenerator_RegistersAKeyedGenerator()
    {
        ServiceCollection services = new();
        NewBuilder(services).AddEmbeddingGenerator("up", "text-embed-3", "search");
        ServiceProvider provider = services.BuildServiceProvider();

        FakeEmbeddingGenerator fake = (FakeEmbeddingGenerator)provider
            .GetRequiredKeyedService<IEmbeddingGenerator<string, Embedding<float>>>("search");

        Assert.Equal("text-embed-3", fake.ModelId);
        Assert.Null(provider.GetService<IEmbeddingGenerator<string, Embedding<float>>>());
    }

    [Fact]
    public void AddEmbeddingGenerator_IsLazy()
    {
        ServiceCollection services = new();
        NewBuilder(services).AddEmbeddingGenerator("up", "text-embed-3");

        ServiceProvider provider = services.BuildServiceProvider();

        Assert.Equal(0, _creations);
        provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        Assert.Equal(1, _creations);
    }

    [Fact]
    public void AddEmbeddingGenerator_ForAnUnknownProvider_FailsOnResolve()
    {
        ServiceCollection services = new();
        NewBuilder(services).AddEmbeddingGenerator("nope", "text-embed-3");
        ServiceProvider provider = services.BuildServiceProvider();

        InvalidOperationException e = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>());

        Assert.Equal("No upstream provider 'nope' is configured. Configured providers: up.", e.Message);
    }

    [Theory]
    [InlineData("", "m")]
    [InlineData(" ", "m")]
    [InlineData("up", "")]
    [InlineData("up", " ")]
    public void AddEmbeddingGenerator_Throws_ForBlankArguments(string providerId, string modelId)
    {
        ServiceCollection services = new();
        ProviderConfigBuilder builder = NewBuilder(services);

        Assert.ThrowsAny<ArgumentException>(() => builder.AddEmbeddingGenerator(providerId, modelId));
    }

    [Fact]
    public void EmbeddingGeneratorFactory_ResolvesGeneratorDirectly()
    {
        ServiceCollection services = new();
        NewBuilder(services);
        ServiceProvider provider = services.BuildServiceProvider();

        IEmbeddingGeneratorFactory factory = provider.GetRequiredService<IEmbeddingGeneratorFactory>();
        IEmbeddingGenerator<string, Embedding<float>> generator = factory.GetEmbeddingGenerator("up", "text-embed-3");

        FakeEmbeddingGenerator fake = Assert.IsType<FakeEmbeddingGenerator>(generator);
        Assert.Equal("up", fake.ProviderId);
        Assert.Equal("text-embed-3", fake.ModelId);
    }
}
