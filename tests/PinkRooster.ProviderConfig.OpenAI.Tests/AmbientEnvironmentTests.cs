using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PinkRooster.ProviderConfig.OpenAI;
using PinkRooster.ProviderConfig.Tests.TestSupport;

namespace PinkRooster.ProviderConfig.OpenAI.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AmbientEnvironmentCollection
{
    public const string Name = "Ambient environment";
}

[Collection(AmbientEnvironmentCollection.Name)]
public sealed class AmbientEnvironmentTests
{
    private static readonly string[] Variables =
        ["OPENAI_API_KEY", "OPENAI_BASE_URL"];

    private const string OpenAIReply = """{"id":"c1","object":"chat.completion","created":1,"model":"m1","choices":[{"index":0,"message":{"role":"assistant","content":"hi"},"finish_reason":"stop"}]}""";

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
        services.AddProviderConfig().AddOpenAI().Configure(o => o.Providers["up"] = options);
        services.AddSingleton(new ProviderHttpHandler(stub));
        return services.BuildServiceProvider().GetRequiredService<IChatClientFactory>().GetChatClient("up", "m1");
    }

    [Fact]
    public Task OpenAI_IgnoresAmbientEnvironment() => WithAmbient(async () =>
    {
        StubUpstream stub = new StubUpstream().RespondJson(OpenAIReply);
        IChatClient chat = Build(stub, new ProviderOptions { Type = "openai", ApiKey = "k1" });

        await chat.GetResponseAsync("q");

        Assert.Equal("api.openai.com", stub.Requests[0].Uri.Host);
        Assert.Equal("Bearer k1", stub.Requests[0].Headers["Authorization"]);
    });
}
