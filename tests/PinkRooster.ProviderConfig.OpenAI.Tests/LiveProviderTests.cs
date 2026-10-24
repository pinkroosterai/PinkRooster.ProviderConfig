using System.ClientModel.Primitives;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI.Responses;
using PinkRooster.ProviderConfig.OpenAI;

namespace PinkRooster.ProviderConfig.OpenAI.Tests;

/// <summary>Calls the real upstream services. Skipped unless the environment variables of a provider are set.</summary>
public sealed class LiveProviderTests
{
    [Fact]
    public async Task OpenAI_ReturnsText()
    {
        (string key, string model) = Require("OPENAI");

        ChatResponse response = await Client("openai", key, model).GetResponseAsync("Say hello.");

        Assert.False(string.IsNullOrWhiteSpace(response.Text));
    }

    [Fact]
    public async Task OpenAIResponses_ReturnsText_WithoutStoringIt()
    {
        (string key, string model) = Require("OPENAI");

        ChatResponse response = await Client("openai_responses", key, model).GetResponseAsync("Say hello.");

        Assert.False(string.IsNullOrWhiteSpace(response.Text));
        // OpenAI 2.14.0 has no StoredOutputEnabled on ResponseResult (only on CreateResponseOptions), so read the echoed "store" from its JSON.
        Assert.Null(response.ConversationId);
        JsonObject raw = (JsonObject)JsonNode.Parse(ModelReaderWriter.Write((ResponseResult)response.RawRepresentation!))!;
        Assert.False(raw["store"]!.GetValue<bool>());
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
        services.AddProviderConfig().AddOpenAI().Configure(o => o.Providers["live"] = new ProviderOptions { Type = type, ApiKey = key });
        return services.BuildServiceProvider().GetRequiredService<IChatClientFactory>().GetChatClient("live", model);
    }
}
