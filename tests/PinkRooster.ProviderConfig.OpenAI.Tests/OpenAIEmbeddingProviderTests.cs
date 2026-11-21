using System.ClientModel;
using System.Net;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
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
    [InlineData("openai_embedding", null)]
    [InlineData("openai_embedding", 0)]
    [InlineData("openai_embeddings", null)]
    [InlineData("openai_embeddings", 0)]
    public async Task OpenAI_PostsEmbeddings_WithKeyAndModel(string type, int? maxRetries)
    {
        IEmbeddingGenerator<string, Embedding<float>> generator = Build(new ProviderOptions
        {
            Type = type,
            MaxRetries = maxRetries,
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

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task OpenAI_ForwardsConfiguredDimensions(int? maxRetries)
    {
        ProviderOptions options = new()
        {
            Type = "openai_embedding",
            MaxRetries = maxRetries,
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

    [Theory]
    [InlineData("openai_embedding", 0, 1)]
    [InlineData("openai_embeddings", 0, 1)]
    [InlineData("openai_embedding", 1, 2)]
    [InlineData("openai_embeddings", 1, 2)]
    public async Task OpenAI_StopsAfterTheConfiguredAttempts(string type, int maxRetries, int attempts)
    {
        using StubUpstream stub = RateLimited(attempts);
        ServiceCollection services = new();
        AddStubbedOpenAI(services, stub).Configure(options =>
            options.Providers["up"] = Provider(type, maxRetries));
        using ServiceProvider provider = services.BuildServiceProvider();

        await AssertEmbeddingFailure(Embedding(provider), HttpStatusCode.TooManyRequests);

        Assert.Equal(attempts, stub.Requests.Count);
    }

    [Theory]
    [InlineData("openai_embedding")]
    [InlineData("openai_embeddings")]
    public async Task OpenAI_RetriesOnceThenSucceeds(string type)
    {
        using StubUpstream stub = RateLimited(1).RespondJson(Reply);
        ServiceCollection services = new();
        AddStubbedOpenAI(services, stub).Configure(options =>
            options.Providers["up"] = Provider(type, 1));
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Single(await Embedding(provider).GenerateAsync(["hello"], cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(2, stub.Requests.Count);
    }

    [Theory]
    [InlineData(false, null, 4)]
    [InlineData(true, null, 4)]
    [InlineData(true, "0", 1)]
    public async Task OpenAI_HonorsConfiguredZeroAndPreservesDefaultForNullOrOmitted(bool supplied, string? value, int attempts)
    {
        using StubUpstream stub = RateLimited(attempts);
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        Dictionary<string, string?> config = new()
        {
            ["Providers:up:Type"] = "openai_embedding",
            ["Providers:up:Endpoint"] = "http://up/v1",
        };
        if (supplied)
        {
            config["Providers:up:MaxRetries"] = value;
        }

        builder.Configuration.AddInMemoryCollection(config);
        AddStubbedOpenAI(builder.Services, stub);
        using IHost host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        await AssertEmbeddingFailure(Embedding(host.Services), HttpStatusCode.TooManyRequests);

        Assert.Equal(attempts, stub.Requests.Count);
    }

    [Fact]
    public async Task OpenAI_DoesNotRetryBadRequests()
    {
        using StubUpstream stub = new StubUpstream().RespondStatus(HttpStatusCode.BadRequest, Error);
        ServiceCollection services = new();
        AddStubbedOpenAI(services, stub).Configure(options =>
            options.Providers["up"] = Provider("openai_embedding", 1));
        using ServiceProvider provider = services.BuildServiceProvider();

        await AssertEmbeddingFailure(Embedding(provider), HttpStatusCode.BadRequest);

        Assert.Single(stub.Requests);
    }

    [Fact]
    public async Task OpenAI_CancelsDuringTheRetryWait()
    {
        using StubUpstream stub = new StubUpstream().RespondStatus(HttpStatusCode.TooManyRequests, Error, retryAfter: TimeSpan.FromMinutes(1));
        ServiceCollection services = new();
        AddStubbedOpenAI(services, stub).Configure(options =>
            options.Providers["up"] = Provider("openai_embedding", 1));
        using ServiceProvider provider = services.BuildServiceProvider();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        // The stub returns the first response synchronously; the SDK retry delay leaves the operation pending.
        Task<GeneratedEmbeddings<Embedding<float>>> pending = Embedding(provider).GenerateAsync(["hello"], cancellationToken: cancellation.Token);
        Assert.Single(stub.Requests);
        Assert.False(pending.IsCompleted);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        Assert.Single(stub.Requests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public async Task OpenAI_PreservesTheConfiguredNetworkTimeout(int? maxRetries)
    {
        using StubUpstream stub = new StubUpstream().Hang().RespondJson(Reply);
        ServiceCollection services = new();
        ProviderOptions options = Provider("openai_embedding", maxRetries);
        options.Timeout = TimeSpan.FromMilliseconds(500);
        AddStubbedOpenAI(services, stub).Configure(o => o.Providers["up"] = options);
        using ServiceProvider provider = services.BuildServiceProvider();

        Task<GeneratedEmbeddings<Embedding<float>>> pending = Embedding(provider)
            .GenerateAsync(["hello"], cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(await stub.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        if (maxRetries is null)
        {
            // The default policy retries this transport timeout; the second attempt succeeds.
            Assert.Single(await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.Equal(2, stub.Requests.Count);
        }
        else
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.Single(stub.Requests);
        }
    }

    [Fact]
    public async Task OpenAI_EmbeddingCapDoesNotChangeEitherChatRetryPolicy()
    {
        using StubUpstream stub = RateLimited(9);
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        AddStubbedOpenAI(builder.Services, stub).Configure(options =>
        {
            options.Providers["up"] = Provider("openai_embedding", 0);
            options.Providers["chat"] = Provider("openai");
            options.Providers["responses"] = Provider("openai_responses");
        });
        using IHost host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        IChatClientFactory chat = host.Services.GetRequiredService<IChatClientFactory>();

        await AssertEmbeddingFailure(Embedding(host.Services), HttpStatusCode.TooManyRequests);
        foreach (string id in new[] { "chat", "responses" })
        {
            using IChatClient client = chat.GetChatClient(id, "model");
            ClientResultException error = await Assert.ThrowsAsync<ClientResultException>(
                () => client.GetResponseAsync("hello", cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(429, error.Status);
        }

        Assert.Equal(9, stub.Requests.Count);
        Assert.Single(stub.Requests, request => request.Uri.AbsolutePath == "/v1/embeddings");
        Assert.Equal(4, stub.Requests.Count(request => request.Uri.AbsolutePath == "/v1/chat/completions"));
        Assert.Equal(4, stub.Requests.Count(request => request.Uri.AbsolutePath == "/v1/responses"));
    }

    [Theory]
    [InlineData("openai", "0")]
    [InlineData("openai_responses", "0")]
    [InlineData("openai_embedding", "-1")]
    [InlineData("openai_embeddings", "invalid")]
    public async Task OpenAI_InvalidMaxRetriesFailsStartupBeforeAnySend(string type, string value)
    {
        using StubUpstream stub = new();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Providers:up:Type"] = type,
            ["Providers:up:Endpoint"] = "http://up/v1",
            ["Providers:up:MaxRetries"] = value,
        });
        AddStubbedOpenAI(builder.Services, stub);
        using IHost host = builder.Build();

        OptionsValidationException error = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("Providers:up:MaxRetries", error.Message);
        Assert.Empty(stub.Requests);
    }

    private const string Error = """{"error":{"message":"upstream failure","type":"test_error"}}""";

    private static ProviderOptions Provider(string type, int? maxRetries = null) => new()
    {
        Type = type,
        Endpoint = new Uri("http://up/v1"),
        MaxRetries = maxRetries,
    };

    private static ProviderConfigBuilder AddStubbedOpenAI(IServiceCollection services, StubUpstream stub)
    {
        services.AddSingleton(new ProviderHttpHandler(stub));
        return services.AddProviderConfig().AddOpenAI();
    }

    private static IEmbeddingGenerator<string, Embedding<float>> Embedding(IServiceProvider services) =>
        services.GetRequiredService<IEmbeddingGeneratorFactory>().GetEmbeddingGenerator("up", "text-embedding-3-small");

    private static StubUpstream RateLimited(int attempts)
    {
        StubUpstream stub = new();
        for (int i = 0; i < attempts; i++)
        {
            stub.RespondStatus(HttpStatusCode.TooManyRequests, Error);
        }

        return stub;
    }

    private static async Task AssertEmbeddingFailure(IEmbeddingGenerator<string, Embedding<float>> generator, HttpStatusCode status)
    {
        ClientResultException error = await Assert.ThrowsAsync<ClientResultException>(
            () => generator.GenerateAsync(["hello"], cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal((int)status, error.Status);
        Assert.Contains("upstream failure", error.Message);
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
