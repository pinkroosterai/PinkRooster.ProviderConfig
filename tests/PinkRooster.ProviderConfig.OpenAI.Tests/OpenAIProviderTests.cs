using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PinkRooster.ProviderConfig.OpenAI;
using PinkRooster.ProviderConfig.Tests.TestSupport;

namespace PinkRooster.ProviderConfig.OpenAI.Tests;

public sealed class OpenAIProviderTests
{
    private const string Reply = """{"id":"c1","object":"chat.completion","created":1,"model":"m1","choices":[{"index":0,"message":{"role":"assistant","content":"hi"},"finish_reason":"stop"}]}""";

    private readonly StubUpstream _stub = new StubUpstream().RespondJson(Reply).RespondJson(Reply);

    private IChatClientFactory Build(ProviderOptions options)
    {
        ServiceCollection services = new();
        services.AddProviderConfig().AddOpenAI().Configure(o => o.Providers["up"] = options);
        services.AddSingleton(new ProviderHttpHandler(_stub));
        return services.BuildServiceProvider().GetRequiredService<IChatClientFactory>();
    }

    [Fact]
    public async Task OpenAI_PostsChatCompletions_WithKeyAndModel()
    {
        IChatClient chat = Build(new ProviderOptions { Type = "openai", ApiKey = "k1", Endpoint = new Uri("http://up/v1") }).GetChatClient("up", "m1");

        Assert.Equal("hi", (await chat.GetResponseAsync("q")).Text);

        Assert.Equal(new Uri("http://up/v1/chat/completions"), _stub.Requests[0].Uri);
        Assert.Equal("Bearer k1", _stub.Requests[0].Headers["Authorization"]);
        Assert.Equal("m1", (string?)_stub.Requests[0].Body!["model"]);
    }

    [Theory]
    [InlineData("https://res.openai.azure.com/openai/v1")]
    [InlineData("https://res.openai.azure.com/openai/v1/")]
    public async Task OpenAI_KeepsTheEndpointPath(string endpoint)
    {
        IChatClient chat = Build(new ProviderOptions { Type = "openai", ApiKey = "k", Endpoint = new Uri(endpoint) }).GetChatClient("up", "m1");

        await chat.GetResponseAsync("q");

        Assert.Equal(new Uri("https://res.openai.azure.com/openai/v1/chat/completions"), _stub.Requests[0].Uri);
    }

    [Theory]
    [InlineData("http://up/v1?api-version=1")]
    [InlineData("http://up/v1/?api-version=1")]
    public async Task OpenAI_KeepsTheEndpointQuery(string endpoint)
    {
        IChatClient chat = Build(new ProviderOptions { Type = "openai", ApiKey = "k", Endpoint = new Uri(endpoint) }).GetChatClient("up", "m1");

        await chat.GetResponseAsync("q");

        Assert.Equal(new Uri("http://up/v1/chat/completions?api-version=1"), _stub.Requests[0].Uri);
    }

    [Fact]
    public async Task OpenAI_DefaultsToTheOpenAIEndpoint()
    {
        IChatClient chat = Build(new ProviderOptions { Type = "openai", ApiKey = "k" }).GetChatClient("up", "m1");

        await chat.GetResponseAsync("q");

        Assert.Equal(new Uri("https://api.openai.com/v1/chat/completions"), _stub.Requests[0].Uri);
    }

    [Fact]
    public async Task OpenAI_SendsAPlaceholder_WhenACustomEndpointHasNoKey()
    {
        IChatClient chat = Build(new ProviderOptions { Type = "openai", Endpoint = new Uri("http://up/v1") }).GetChatClient("up", "m1");

        await chat.GetResponseAsync("q");

        Assert.Equal("Bearer providerconfig-no-key", _stub.Requests[0].Headers["Authorization"]);
    }

    [Fact]
    public async Task OpenAI_ModelOptionOverridesTheDefault()
    {
        IChatClient chat = Build(new ProviderOptions { Type = "openai", ApiKey = "k", Endpoint = new Uri("http://up/v1") }).GetChatClient("up", "m1");

        await chat.GetResponseAsync("q", new ChatOptions { ModelId = "m2" });

        Assert.Equal("m2", (string?)_stub.Requests[0].Body!["model"]);
    }

    [Fact]
    public async Task OpenAI_DisposingTheClient_LeavesTheProviderUsable()
    {
        IChatClientFactory factory = Build(new ProviderOptions { Type = "openai", ApiKey = "k", Endpoint = new Uri("http://up/v1") });
        IChatClient first = factory.GetChatClient("up", "m1");
        await first.GetResponseAsync("q");
        first.Dispose();

        IChatClient second = factory.GetChatClient("up", "m1");

        Assert.Equal("hi", (await second.GetResponseAsync("q")).Text);
    }

    [Fact]
    public void OpenAI_GetChatClient_MakesNoRequest()
    {
        Build(new ProviderOptions { Type = "openai", ApiKey = "k", Endpoint = new Uri("http://up/v1") }).GetChatClient("up", "m1");

        Assert.Empty(_stub.Requests);
    }
}
