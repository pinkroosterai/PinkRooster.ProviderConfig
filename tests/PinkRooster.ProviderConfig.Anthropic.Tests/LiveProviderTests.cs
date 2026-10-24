using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace PinkRooster.ProviderConfig.Anthropic.Tests;

/// <summary>Calls the real upstream service. Skipped unless the environment variables of the provider are set.</summary>
public sealed class LiveProviderTests
{
    [Fact]
    public async Task Anthropic_ReturnsText()
    {
        (string key, string model) = Require("ANTHROPIC");

        ChatResponse response = await Client("anthropic", key, model).GetResponseAsync("Say hello.");

        Assert.False(string.IsNullOrWhiteSpace(response.Text));
    }

    private static (string Key, string Model) Require(string provider)
    {
        string? key = Environment.GetEnvironmentVariable($"PROVIDERCONFIG_LIVE_{provider}_KEY");
        string? model = Environment.GetEnvironmentVariable($"PROVIDERCONFIG_LIVE_{provider}_MODEL");
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(model))
        {
            Assert.Skip($"Set PROVIDERCONFIG_LIVE_{provider}_KEY and PROVIDERCONFIG_LIVE_{provider}_MODEL to run this test.");
        }

        return (key, model);
    }

    private static IChatClient Client(string type, string key, string model)
    {
        ServiceCollection services = new();
        services.AddProviderConfig().AddAnthropic().Configure(o => o.Providers["live"] = new ProviderOptions { Type = type, ApiKey = key });
        return services.BuildServiceProvider().GetRequiredService<IChatClientFactory>().GetChatClient("live", model);
    }
}
