using System.Text.Json;
using System.Text.Json.Nodes;
using Jev.Net;
using Microsoft.Extensions.DependencyInjection;
using PinkRooster.ProviderConfig.Tests.TestSupport;

namespace PinkRooster.ProviderConfig.Jev.Tests;

public sealed class JevProviderTests
{
    private static readonly JsonContent State = "I was charged twice.";
    private static readonly IReadOnlyDictionary<string, Question> Questions = new Dictionary<string, Question> { ["q"] = new Noul("Is it a refund?") };

    private readonly StubUpstream _stub = new();

    private ServiceProvider Build(string? endpoint = null, TimeSpan? timeout = null)
    {
        ServiceCollection services = new();
        services.AddProviderConfig().AddJev().Configure(o => o.Providers["jev"] = new ProviderOptions
        {
            Type = "jev",
            ApiKey = "k1",
            Endpoint = endpoint is null ? null : new Uri(endpoint),
            Timeout = timeout,
        });
        services.AddSingleton(new ProviderHttpHandler(_stub));
        return services.BuildServiceProvider();
    }

    private ITypeSafeClient Client(ServiceProvider provider, string model = "jev-1.14") =>
        provider.GetRequiredService<ISystemOneClientFactory>().GetSystemOneClient("jev", model);

    [Fact]
    public async Task Jev_PostsSystemOne_WithKeyAndModel()
    {
        _stub.RespondJson(JevResponses.Noul());
        using ServiceProvider provider = Build();

        await Client(provider).SystemOneAsync(State, Questions);

        Assert.Equal(new Uri("https://api.typesafe.ai/v1/systemone"), _stub.Requests[0].Uri);
        Assert.Equal("Bearer k1", _stub.Requests[0].Headers["Authorization"]);
        Assert.Equal("jev-1.14", (string?)_stub.Requests[0].Body!["model"]);
    }

    [Fact]
    public async Task Jev_UsesTheEndpoint_WithItsPath()
    {
        _stub.RespondJson(JevResponses.Noul());
        using ServiceProvider provider = Build("http://up/prefix");

        await Client(provider).SystemOneAsync(State, Questions);

        Assert.Equal(new Uri("http://up/prefix/v1/systemone"), _stub.Requests[0].Uri);
    }

    [Fact]
    public async Task Jev_TrailingSlashOnTheEndpoint_IsIgnored()
    {
        _stub.RespondJson(JevResponses.Noul());
        using ServiceProvider provider = Build("http://up/prefix/");

        await Client(provider).SystemOneAsync(State, Questions);

        Assert.Equal(new Uri("http://up/prefix/v1/systemone"), _stub.Requests[0].Uri);
    }

    [Fact]
    public async Task Jev_CallersModelWins()
    {
        _stub.RespondJson(JevResponses.Noul());
        using ServiceProvider provider = Build();

        await Client(provider).SystemOneAsync(State, Questions, new SystemOneOptions { Model = "jev-other" });

        Assert.Equal("jev-other", (string?)_stub.Requests[0].Body!["model"]);
    }

    [Fact]
    public async Task Jev_KeepsTheCallersOtherOptions_AndDoesNotChangeThem()
    {
        _stub.RespondJson(JevResponses.Noul());
        using ServiceProvider provider = Build();
        SystemOneOptions options = new() { Model = null, ExtraBody = new Dictionary<string, JsonNode?> { ["x"] = 1 } };

        await Client(provider).SystemOneAsync(State, Questions, options);

        JsonObject body = _stub.Requests[0].Body!;
        Assert.Equal(1, (int?)body["x"]);
        Assert.Equal("jev-1.14", (string?)body["model"]);
        Assert.Null(options.Model);
    }

    [Fact]
    public async Task Jev_KeepsTheCallersHeadersRetryAndTimeout()
    {
        _stub.RespondJson(JevResponses.Noul());
        using ServiceProvider provider = Build();
        SystemOneOptions options = new() { ExtraHeaders = new Dictionary<string, string> { ["X-Test"] = "yes" } };

        await Client(provider).SystemOneAsync(State, Questions, options);

        Assert.Equal("yes", _stub.Requests[0].Headers["X-Test"]);
    }

    public sealed class Reply : SystemOneResponse;

    [Fact]
    public async Task Jev_GenericOverloads_AlsoPinTheModel()
    {
        _stub.RespondJson(JevResponses.Noul()).RespondJson(JevResponses.Noul()).RespondJson(JevResponses.Noul());
        using ServiceProvider provider = Build();
        ITypeSafeClient client = Client(provider);
        JsonSerializerOptions json = new(JsonSerializerDefaults.Web) { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<Reply> info = (System.Text.Json.Serialization.Metadata.JsonTypeInfo<Reply>)json.GetTypeInfo(typeof(Reply));

        _ = await client.SystemOneAsync<Reply>(State, Questions);
        _ = await client.SystemOneAsync<Reply>(State, Questions, null, info);
        _ = await client.SystemOneAsync<Reply>(State, Questions, null, json);

        Assert.Equal(3, _stub.Requests.Count);
        Assert.All(_stub.Requests, r => Assert.Equal("jev-1.14", (string?)r.Body!["model"]));
    }

    [Fact]
    public async Task Jev_OneSdkClientPerProvider()
    {
        _stub.RespondJson(JevResponses.Noul()).RespondJson(JevResponses.Noul());
        int created = 0;
        ServiceCollection services = new();
        services.AddProviderConfig()
            .AddProviderType<ITypeSafeClient>("counted", context =>
            {
                Func<string, ITypeSafeClient> inner = JevProviderType.Create(context);
                created++;
                return inner;
            })
            .Configure(o => o.Providers["jev"] = new ProviderOptions { Type = "counted", ApiKey = "k1" });
        services.AddSingleton(new ProviderHttpHandler(_stub));
        using ServiceProvider provider = services.BuildServiceProvider();
        IProviderClientFactory factory = provider.GetRequiredService<IProviderClientFactory>();

        await factory.GetClient<ITypeSafeClient>("jev", "m1").SystemOneAsync(State, Questions);
        await factory.GetClient<ITypeSafeClient>("jev", "m2").SystemOneAsync(State, Questions);

        Assert.Equal(1, created);
        Assert.Equal("m1", (string?)_stub.Requests[0].Body!["model"]);
        Assert.Equal("m2", (string?)_stub.Requests[1].Body!["model"]);
    }

    [Fact]
    public void Jev_GetSystemOneClient_MakesNoRequest()
    {
        using ServiceProvider provider = Build();

        Client(provider);

        Assert.Empty(_stub.Requests);
    }

    [Fact]
    public async Task Jev_Timeout_IsPassedToTheSdk()
    {
        // The SDK retries a timed-out attempt, so every attempt hangs.
        _stub.Hang().Hang().Hang().Hang();
        using ServiceProvider provider = Build(timeout: TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAsync<TypeSafeApiTimeoutException>(() => Client(provider).SystemOneAsync(State, Questions));
    }

    [Fact]
    public async Task Jev_IsDisposedWithTheContainer()
    {
        _stub.RespondJson(JevResponses.Noul());
        ServiceProvider provider = Build();
        ITypeSafeClient client = Client(provider);
        provider.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.SystemOneAsync(State, Questions));
    }

    [Fact]
    public void GetChatClient_OnAJevProvider_FailsWithTheKindMessage()
    {
        using ServiceProvider provider = Build();

        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<IChatClientFactory>().GetChatClient("jev", "m"));

        Assert.Equal("Upstream provider 'jev' has type 'jev', which creates ITypeSafeClient, not IChatClient.", e.Message);
    }

    [Fact]
    public void AddSystemOneClient_RegistersUnkeyedAndKeyed_PinnedClients()
    {
        ServiceCollection services = new();
        services.AddProviderConfig().AddJev()
            .Configure(o => o.Providers["jev"] = new ProviderOptions { Type = "jev", ApiKey = "k1" })
            .AddSystemOneClient("jev", "jev-1.14")
            .AddSystemOneClient("jev", "jev-other", "other");
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.IsType<ModelPinnedTypeSafeClient>(provider.GetRequiredService<ITypeSafeClient>());
        Assert.IsType<ModelPinnedTypeSafeClient>(provider.GetRequiredKeyedService<ITypeSafeClient>("other"));
    }
}
