using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PinkRooster.ProviderConfig.Tests.TestSupport;

namespace PinkRooster.ProviderConfig.Anthropic.Tests;

public sealed class AnthropicProviderTests
{
    private const string Reply = """{"id":"msg_1","type":"message","role":"assistant","model":"claude-x","content":[{"type":"text","text":"hello"}],"stop_reason":"end_turn","stop_sequence":null,"usage":{"input_tokens":1,"output_tokens":1}}""";

    private readonly StubUpstream _stub = new StubUpstream().RespondJson(Reply);

    private IChatClient Build(string endpoint)
    {
        ServiceCollection services = new();
        services.AddProviderConfig().AddAnthropic().Configure(o => o.Providers["up"] = new ProviderOptions { Type = "anthropic", ApiKey = "k1", Endpoint = new Uri(endpoint) });
        services.AddSingleton(new ProviderHttpHandler(_stub));
        return services.BuildServiceProvider().GetRequiredService<IChatClientFactory>().GetChatClient("up", "claude-x");
    }

    [Fact]
    public async Task Anthropic_PostsMessages_WithKeyVersionAndModel()
    {
        ChatResponse response = await Build("http://up/prefix").GetResponseAsync("q");

        Assert.Equal(new Uri("http://up/prefix/v1/messages"), _stub.Requests[0].Uri);
        Assert.Equal("Bearer k1", _stub.Requests[0].Headers["Authorization"]);
        Assert.Equal("2026-10-21", _stub.Requests[0].Headers["anthropic-version"]);
        Assert.Equal("claude-x", (string?)_stub.Requests[0].Body!["model"]);
        Assert.Equal("hello", response.Text);
    }

    [Fact]
    public async Task Anthropic_TrailingSlashOnTheEndpoint_IsIgnored()
    {
        await Build("http://up/prefix/").GetResponseAsync("q");

        Assert.Equal(new Uri("http://up/prefix/v1/messages"), _stub.Requests[0].Uri);
    }

    [Fact]
    public void Anthropic_GetChatClient_MakesNoRequest()
    {
        Build("http://up/prefix");

        Assert.Empty(_stub.Requests);
    }

    [Fact]
    public async Task Anthropic_SendsTheDefaultMaxTokens()
    {
        await Build("http://up/prefix").GetResponseAsync("q");

        Assert.Equal(1024, (int?)_stub.Requests[0].Body!["max_tokens"]);
    }

    [Fact]
    public async Task Anthropic_DisposingTheClient_LeavesTheProviderUsable()
    {
        _stub.RespondJson(Reply);
        ServiceCollection services = new();
        services.AddProviderConfig().AddAnthropic().Configure(o => o.Providers["up"] = new ProviderOptions { Type = "anthropic", ApiKey = "k1", Endpoint = new Uri("http://up/prefix") });
        services.AddSingleton(new ProviderHttpHandler(_stub));
        IChatClientFactory factory = services.BuildServiceProvider().GetRequiredService<IChatClientFactory>();
        IChatClient first = factory.GetChatClient("up", "claude-x");
        await first.GetResponseAsync("q");
        first.Dispose();

        IChatClient second = factory.GetChatClient("up", "claude-x");

        Assert.Equal("hello", (await second.GetResponseAsync("q")).Text);
    }
}
