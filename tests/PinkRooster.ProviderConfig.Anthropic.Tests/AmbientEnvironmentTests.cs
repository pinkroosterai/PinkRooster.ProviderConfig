using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PinkRooster.ProviderConfig.Tests.TestSupport;

namespace PinkRooster.ProviderConfig.Anthropic.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AmbientEnvironmentCollection
{
    public const string Name = "Ambient environment";
}

[Collection(AmbientEnvironmentCollection.Name)]
public sealed class AmbientEnvironmentTests
{
    private static readonly string[] Variables =
        ["ANTHROPIC_API_KEY", "ANTHROPIC_AUTH_TOKEN", "ANTHROPIC_BASE_URL"];

    private const string AnthropicReply = """{"id":"msg_1","type":"message","role":"assistant","model":"claude-x","content":[{"type":"text","text":"hello"}],"stop_reason":"end_turn","stop_sequence":null,"usage":{"input_tokens":1,"output_tokens":1}}""";

    private static async Task WithAmbient(Func<Task> body)
    {
        Dictionary<string, string?> previous = Variables.ToDictionary(v => v, Environment.GetEnvironmentVariable);
        try
        {
            foreach (string name in Variables)
            {
                Environment.SetEnvironmentVariable(name, name.EndsWith("BASE_URL") ? "http://ambient" : "ambient");
            }

            await body();
        }
        finally
        {
            foreach ((string name, string? value) in previous)
            {
                Environment.SetEnvironmentVariable(name, value);
            }
        }
    }

    private static IChatClient Build(StubUpstream stub, ProviderOptions options)
    {
        ServiceCollection services = new();
        services.AddProviderConfig().AddAnthropic().Configure(o => o.Providers["up"] = options);
        services.AddSingleton(new ProviderHttpHandler(stub));
        return services.BuildServiceProvider().GetRequiredService<IChatClientFactory>().GetChatClient("up", "claude-x");
    }

    [Fact]
    public Task Anthropic_IgnoresAmbientEnvironment() => WithAmbient(async () =>
    {
        StubUpstream stub = new StubUpstream().RespondJson(AnthropicReply);
        IChatClient chat = Build(stub, new ProviderOptions { Type = "anthropic", ApiKey = "k1", Endpoint = new Uri("http://up/prefix") });

        await chat.GetResponseAsync("q");

        Assert.Equal("up", stub.Requests[0].Uri.Host);
        Assert.Equal("Bearer k1", stub.Requests[0].Headers["Authorization"]);
    });
}
