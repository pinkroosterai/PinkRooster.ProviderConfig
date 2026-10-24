using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PinkRooster.ProviderConfig.Tests.TestSupport;

namespace PinkRooster.ProviderConfig.Tests.Builders;

public sealed class AddChatClientTests
{
    private int _creations;

    private ProviderConfigBuilder NewBuilder(ServiceCollection services) =>
        services.AddProviderConfig()
            .AddProviderType<IChatClient>("fake", context =>
            {
                Interlocked.Increment(ref _creations);
                return model => new FakeChatClient(context.ProviderId, model);
            })
            .Configure(options => options.Providers["up"] = new ProviderOptions { Type = "fake" });

    [Fact]
    public void AddChatClient_RegistersAnUnkeyedClient_ForTheModel()
    {
        ServiceCollection services = new();
        NewBuilder(services).AddChatClient("up", "m1");

        FakeChatClient fake = (FakeChatClient)services.BuildServiceProvider().GetRequiredService<IChatClient>();

        Assert.Equal("up", fake.ProviderId);
        Assert.Equal("m1", fake.ModelId);
    }

    [Fact]
    public void AddChatClient_RegistersAKeyedClient()
    {
        ServiceCollection services = new();
        NewBuilder(services).AddChatClient("up", "m1", "fast");
        ServiceProvider provider = services.BuildServiceProvider();

        FakeChatClient fake = (FakeChatClient)provider.GetRequiredKeyedService<IChatClient>("fast");

        Assert.Equal("m1", fake.ModelId);
        Assert.Null(provider.GetService<IChatClient>());
    }

    [Fact]
    public void AddChatClient_IsLazy()
    {
        ServiceCollection services = new();
        NewBuilder(services).AddChatClient("up", "m1");

        ServiceProvider provider = services.BuildServiceProvider();

        Assert.Equal(0, _creations);
        provider.GetRequiredService<IChatClient>();
        Assert.Equal(1, _creations);
    }

    [Fact]
    public void AddChatClient_ForAnUnknownProvider_FailsOnResolve()
    {
        ServiceCollection services = new();
        NewBuilder(services).AddChatClient("nope", "m1");
        ServiceProvider provider = services.BuildServiceProvider();

        InvalidOperationException e = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IChatClient>());

        Assert.Equal("No upstream provider 'nope' is configured. Configured providers: up.", e.Message);
    }

    [Theory]
    [InlineData("", "m")]
    [InlineData(" ", "m")]
    [InlineData("up", "")]
    [InlineData("up", " ")]
    public void AddChatClient_Throws_ForBlankArguments(string providerId, string modelId)
    {
        ServiceCollection services = new();
        ProviderConfigBuilder builder = NewBuilder(services);

        Assert.ThrowsAny<ArgumentException>(() => builder.AddChatClient(providerId, modelId));
    }
}
