using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PinkRooster.ProviderConfig.Tests.TestSupport;

namespace PinkRooster.ProviderConfig.Tests.Factories;

public sealed class ProviderClientFactoryTests
{
    private int _creations;

    private IChatClientFactory Build(params string[] providerIds)
    {
        ServiceCollection services = new();
        services.AddProviderConfig()
            .AddProviderType<IChatClient>("fake", context =>
            {
                Interlocked.Increment(ref _creations);
                Thread.Sleep(20);
                return model => new FakeChatClient(context.ProviderId, model);
            })
            .Configure(options =>
            {
                foreach (string id in providerIds)
                {
                    options.Providers[id] = new ProviderOptions { Type = "fake" };
                }
            });
        return services.BuildServiceProvider().GetRequiredService<IChatClientFactory>();
    }

    [Fact]
    public void GetChatClient_ReturnsTheTypesClient_ForTheModel()
    {
        FakeChatClient fake = (FakeChatClient)Build("up").GetChatClient("up", "m1");

        Assert.Equal("up", fake.ProviderId);
        Assert.Equal("m1", fake.ModelId);
    }

    [Fact]
    public void GetChatClient_FindsTheProvider_IgnoringCase()
    {
        IChatClientFactory factory = Build("Up");

        FakeChatClient fake = (FakeChatClient)factory.GetChatClient("UP", "m1");

        Assert.Equal("Up", fake.ProviderId);
        Assert.Equal("m1", fake.ModelId);
        Assert.Equal("Up", ((FakeChatClient)factory.GetChatClient("up", "m2")).ProviderId);
    }

    [Fact]
    public async Task GetChatClient_CreatesOneClientPerProvider_UnderConcurrency()
    {
        IChatClientFactory factory = Build("up");

        await Task.WhenAll(Enumerable.Range(0, 64).Select(i => Task.Run(() => factory.GetChatClient("up", $"m{i}"))));

        Assert.Equal(1, _creations);
    }

    [Fact]
    public void GetChatClient_Throws_ForAnUnknownProvider()
    {
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => Build("up").GetChatClient("nope", "m"));

        Assert.Equal("No upstream provider 'nope' is configured. Configured providers: up.", e.Message);
    }

    [Fact]
    public void GetChatClient_ListsNone_WhenNoProvidersAreConfigured()
    {
        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => Build().GetChatClient("nope", "m"));

        Assert.EndsWith("Configured providers: (none).", e.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void GetChatClient_Throws_ForABlankModel(string model) =>
        Assert.Throws<ArgumentException>(() => Build("up").GetChatClient("up", model));

    [Fact]
    public void AddProviderType_Throws_ForATypeAlreadyRegistered()
    {
        ProviderConfigBuilder builder = new ServiceCollection().AddProviderConfig().AddProviderType<IChatClient>("fake", _ => _ => new FakeChatClient("a", "b"));

        ArgumentException e = Assert.Throws<ArgumentException>(() => builder.AddProviderType<IChatClient>("FAKE", _ => _ => new FakeChatClient("a", "b")));

        Assert.StartsWith("A provider type 'FAKE' is already registered.", e.Message);
        Assert.Equal("type", e.ParamName);
    }

    private sealed class DisposeProbe : IDisposable
    {
        public int Disposals;

        public void Dispose() => Interlocked.Increment(ref Disposals);
    }

    [Fact]
    public void Dispose_DisposesClientsRegisteredByProviderTypes()
    {
        DisposeProbe probe = new();
        ServiceCollection services = new();
        services.AddProviderConfig()
            .AddProviderType<IChatClient>("fake", context =>
            {
                context.RegisterDisposable(probe);
                return model => new FakeChatClient(context.ProviderId, model);
            })
            .Configure(o => o.Providers["up"] = new ProviderOptions { Type = "fake" });
        ServiceProvider provider = services.BuildServiceProvider();
        IChatClientFactory factory = provider.GetRequiredService<IChatClientFactory>();
        factory.GetChatClient("up", "m");
        Assert.Equal(0, probe.Disposals);

        ((IDisposable)factory).Dispose();
        ((IDisposable)factory).Dispose();

        Assert.Equal(1, probe.Disposals);
    }

    private sealed class ThrowingProbe : IDisposable
    {
        public void Dispose() => throw new InvalidOperationException("boom");
    }

    [Fact]
    public void Dispose_DisposesTheRemainingClients_WhenOneThrows_AndDoesNotThrow()
    {
        DisposeProbe probe = new();
        ServiceCollection services = new();
        services.AddProviderConfig()
            .AddProviderType<IChatClient>("fake", context =>
            {
                context.RegisterDisposable(probe);
                context.RegisterDisposable(new ThrowingProbe());
                context.RegisterDisposable(probe);
                return model => new FakeChatClient(context.ProviderId, model);
            })
            .Configure(o => o.Providers["up"] = new ProviderOptions { Type = "fake" });
        ServiceProvider provider = services.BuildServiceProvider();
        IChatClientFactory factory = provider.GetRequiredService<IChatClientFactory>();
        factory.GetChatClient("up", "m");

        Exception? thrown = Record.Exception(() => ((IDisposable)factory).Dispose());
        Assert.Null(thrown);

        // The thrower sits between two probes, so both are disposed only if disposal continues past the failure, whatever the drain order.
        Assert.Equal(2, probe.Disposals);
        ((IDisposable)factory).Dispose();
    }

    [Fact]
    public void Dispose_RunsWhenTheContainerIsDisposed()
    {
        DisposeProbe probe = new();
        ServiceCollection services = new();
        services.AddProviderConfig()
            .AddProviderType<IChatClient>("fake", context =>
            {
                context.RegisterDisposable(probe);
                return model => new FakeChatClient(context.ProviderId, model);
            })
            .Configure(o => o.Providers["up"] = new ProviderOptions { Type = "fake" });
        ServiceProvider provider = services.BuildServiceProvider();
        provider.GetRequiredService<IChatClientFactory>().GetChatClient("up", "m");

        provider.Dispose();

        Assert.Equal(1, probe.Disposals);
    }

    [Fact]
    public void ProviderContext_CarriesIdOptionsAndServices()
    {
        ProviderContext? captured = null;
        ProviderOptions options = new() { Type = "fake" };
        ServiceCollection services = new();
        services.AddProviderConfig()
            .AddProviderType<IChatClient>("fake", context =>
            {
                captured = context;
                return model => new FakeChatClient(context.ProviderId, model);
            })
            .Configure(o => o.Providers["up"] = options);
        ServiceProvider provider = services.BuildServiceProvider();

        provider.GetRequiredService<IChatClientFactory>().GetChatClient("up", "m");

        Assert.NotNull(captured);
        Assert.Equal("up", captured.ProviderId);
        Assert.Same(options, captured.Options);
        Assert.NotNull(captured.Services);
    }

    private sealed class FakeSystemOneClient;

    private static ServiceProvider BuildSingle(Action<ProviderConfigBuilder> register)
    {
        ServiceCollection services = new();
        ProviderConfigBuilder builder = services.AddProviderConfig();
        register(builder);
        builder.Configure(o => o.Providers["p"] = new ProviderOptions { Type = "sys" });
        return services.BuildServiceProvider();
    }

    [Fact]
    public void GetClient_Throws_ForTheWrongClientKind()
    {
        bool ran = false;
        using ServiceProvider provider = BuildSingle(b => b.AddProviderType<FakeSystemOneClient>("sys", _ =>
        {
            ran = true;
            return _ => new FakeSystemOneClient();
        }));

        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IChatClientFactory>().GetChatClient("p", "m"));

        Assert.Equal("Upstream provider 'p' has type 'sys', which creates FakeSystemOneClient, not IChatClient.", e.Message);
        Assert.False(ran);
    }

    [Fact]
    public void GetClient_ReturnsTheRegisteredKind()
    {
        using ServiceProvider provider = BuildSingle(b => b.AddProviderType<FakeSystemOneClient>("sys", _ => _ => new FakeSystemOneClient()));

        Assert.NotNull(provider.GetRequiredService<IProviderClientFactory>().GetClient<FakeSystemOneClient>("p", "m"));
    }

    private static Func<string, IChatClient> CreateStatic(ProviderContext context) => model => new FakeChatClient(context.ProviderId, model);

    [Fact]
    public void AddProviderType_IsANoOp_ForTheSameRegistration()
    {
        ProviderConfigBuilder builder = new ServiceCollection().AddProviderConfig();

        builder.AddProviderType<IChatClient>("fake", CreateStatic);
        Exception? thrown = Record.Exception(() => builder.AddProviderType<IChatClient>("fake", CreateStatic));

        Assert.Null(thrown);
    }

    [Fact]
    public void AddProviderType_Throws_ForTheSameNameWithAnotherClientKind()
    {
        ProviderConfigBuilder builder = new ServiceCollection().AddProviderConfig();
        builder.AddProviderType<IChatClient>("fake", CreateStatic);

        Assert.Throws<ArgumentException>(() => builder.AddProviderType<FakeSystemOneClient>("fake", _ => _ => new FakeSystemOneClient()));
    }

    [Fact]
    public void AddProviderConfig_Twice_SharesOneRegistry()
    {
        ServiceCollection services = new();
        services.AddProviderConfig().AddProviderType<IChatClient>("fake", CreateStatic);
        services.AddProviderConfig().Configure(o => o.Providers["up"] = new ProviderOptions { Type = "fake" });
        using ServiceProvider provider = services.BuildServiceProvider();

        FakeChatClient fake = (FakeChatClient)provider.GetRequiredService<IChatClientFactory>().GetChatClient("up", "m");

        Assert.Equal("up", fake.ProviderId);
    }

    [Fact]
    public void ChatClientFactory_AndProviderClientFactory_AreOneInstance()
    {
        ServiceCollection services = new();
        services.AddProviderConfig();
        services.AddProviderConfig();
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(provider.GetRequiredService<IChatClientFactory>(), provider.GetRequiredService<IProviderClientFactory>());
    }
}
