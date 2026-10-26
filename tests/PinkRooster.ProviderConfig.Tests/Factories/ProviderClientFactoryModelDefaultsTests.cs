using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PinkRooster.ProviderConfig.Tests.TestSupport;

namespace PinkRooster.ProviderConfig.Tests.Factories;

public sealed class ProviderClientFactoryModelDefaultsTests
{
    [Fact]
    public async Task GetChatClient_WhenProviderHasModels_ReturnsWrappedClientApplyingDefaults()
    {
        var services = new ServiceCollection();
        var recordingClient = new RecordingChatClient();

        services.AddProviderConfig()
            .AddProviderType<IChatClient>("fake", _ => _ => recordingClient)
            .Configure(opts =>
            {
                var provider = new ProviderOptions { Type = "fake" };
                provider.Models["gpt-5"] = new ModelOptions { Temperature = 0.25f };
                opts.Providers["p"] = provider;
            });

        using ServiceProvider sp = services.BuildServiceProvider();
        IChatClientFactory factory = sp.GetRequiredService<IChatClientFactory>();

        IChatClient client = factory.GetChatClient("p", "gpt-5");
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);

        Assert.NotNull(recordingClient.LastOptions);
        Assert.Equal(0.25f, recordingClient.LastOptions.Temperature);
    }

    [Fact]
    public void GetChatClient_WhenProviderHasNoModels_ReturnsUnwrappedClient()
    {
        var services = new ServiceCollection();
        var fakeClient = new FakeChatClient("fake", "gpt-5");

        services.AddProviderConfig()
            .AddProviderType<IChatClient>("fake", _ => _ => fakeClient)
            .Configure(opts =>
            {
                opts.Providers["p"] = new ProviderOptions { Type = "fake" };
            });

        using ServiceProvider sp = services.BuildServiceProvider();
        IChatClientFactory factory = sp.GetRequiredService<IChatClientFactory>();

        IChatClient client = factory.GetChatClient("p", "gpt-5");
        Assert.Same(fakeClient, client);
    }

    [Fact]
    public void GetClient_WhenProviderHasModels_ReturnsUnwrappedClient()
    {
        var services = new ServiceCollection();
        var fakeClient = new FakeChatClient("fake", "gpt-5");

        services.AddProviderConfig()
            .AddProviderType<IChatClient>("fake", _ => _ => fakeClient)
            .Configure(opts =>
            {
                var provider = new ProviderOptions { Type = "fake" };
                provider.Models["gpt-5"] = new ModelOptions { Temperature = 0.25f };
                opts.Providers["p"] = provider;
            });

        using ServiceProvider sp = services.BuildServiceProvider();
        IProviderClientFactory factory = sp.GetRequiredService<IProviderClientFactory>();

        IChatClient client = factory.GetClient<IChatClient>("p", "gpt-5");
        Assert.Same(fakeClient, client);
    }
}
