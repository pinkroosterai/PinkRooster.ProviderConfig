using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PinkRooster.ProviderConfig.OpenAI;
using PinkRooster.ProviderConfig.Tests.TestSupport;

namespace PinkRooster.ProviderConfig.OpenAI.Tests;

public sealed class OpenAIEmbeddingProviderTests
{
    private const string Reply = """{"object":"list","data":[{"object":"embedding","index":0,"embedding":[0.1,0.2,0.3]}],"model":"text-embedding-3-small","usage":{"prompt_tokens":2,"total_tokens":2}}""";

    private readonly StubUpstream _stub = new StubUpstream().RespondJson(Reply).RespondJson(Reply);

    private IEmbeddingGeneratorFactory Build(ProviderOptions options)
    {
        ServiceCollection services = new();
        services.AddProviderConfig().AddOpenAI().Configure(o => o.Providers["up"] = options);
        services.AddSingleton(new ProviderHttpHandler(_stub));
        return services.BuildServiceProvider().GetRequiredService<IEmbeddingGeneratorFactory>();
    }

    [Theory]
    [InlineData("openai_embedding")]
    [InlineData("openai_embeddings")]
    public async Task OpenAI_PostsEmbeddings_WithKeyAndModel(string type)
    {
        IEmbeddingGenerator<string, Embedding<float>> generator = Build(new ProviderOptions
        {
            Type = type,
            ApiKey = "k1",
            Endpoint = new Uri("http://up/v1"),
        }).GetEmbeddingGenerator("up", "text-embedding-3-small");

        GeneratedEmbeddings<Embedding<float>> embeddings = await generator.GenerateAsync(["hello"]);

        Assert.Single(embeddings);
        Assert.Equal(3, embeddings[0].Vector.Length);
        Assert.Equal(new Uri("http://up/v1/embeddings"), _stub.Requests[0].Uri);
        Assert.Equal("Bearer k1", _stub.Requests[0].Headers["Authorization"]);
        Assert.Equal("text-embedding-3-small", (string?)_stub.Requests[0].Body!["model"]);
    }

    [Fact]
    public async Task OpenAI_ForwardsConfiguredDimensions()
    {
        ProviderOptions options = new()
        {
            Type = "openai_embedding",
            ApiKey = "k1",
            Endpoint = new Uri("http://up/v1"),
        };
        options.Models["text-embedding-3-small"] = new ModelOptions { Dimensions = 256 };

        IEmbeddingGenerator<string, Embedding<float>> generator = Build(options).GetEmbeddingGenerator("up", "text-embedding-3-small");

        await generator.GenerateAsync(["hello"]);

        Assert.Equal(256, (int)_stub.Requests[0].Body!["dimensions"]!);
    }

    [Fact]
    public async Task OpenAI_OmitsDimensionsWhenUnset()
    {
        ProviderOptions options = new()
        {
            Type = "openai_embedding",
            ApiKey = "k1",
            Endpoint = new Uri("http://up/v1"),
        };

        IEmbeddingGenerator<string, Embedding<float>> generator = Build(options).GetEmbeddingGenerator("up", "text-embedding-3-small");

        await generator.GenerateAsync(["hello"]);

        Assert.False(_stub.Requests[0].Body!.ContainsKey("dimensions"));
    }

    [Fact]
    public async Task OpenAI_SendsAPlaceholder_WhenACustomEndpointHasNoKey()
    {
        IEmbeddingGenerator<string, Embedding<float>> generator = Build(new ProviderOptions
        {
            Type = "openai_embedding",
            Endpoint = new Uri("http://up/v1"),
        }).GetEmbeddingGenerator("up", "text-embedding-3-small");

        await generator.GenerateAsync(["hello"]);

        Assert.Equal("Bearer providerconfig-no-key", _stub.Requests[0].Headers["Authorization"]);
    }

    [Fact]
    public async Task OpenAI_DisposingTheGenerator_LeavesTheProviderUsable()
    {
        IEmbeddingGeneratorFactory factory = Build(new ProviderOptions
        {
            Type = "openai_embedding",
            ApiKey = "k1",
            Endpoint = new Uri("http://up/v1"),
        });

        IEmbeddingGenerator<string, Embedding<float>> first = factory.GetEmbeddingGenerator("up", "text-embedding-3-small");
        await first.GenerateAsync(["hello"]);
        first.Dispose();

        IEmbeddingGenerator<string, Embedding<float>> second = factory.GetEmbeddingGenerator("up", "text-embedding-3-small");
        GeneratedEmbeddings<Embedding<float>> embeddings = await second.GenerateAsync(["hello again"]);

        Assert.Single(embeddings);
    }
}
