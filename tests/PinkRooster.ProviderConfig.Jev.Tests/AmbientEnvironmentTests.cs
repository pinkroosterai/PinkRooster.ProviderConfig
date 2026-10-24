using Jev.Net;
using Microsoft.Extensions.DependencyInjection;
using PinkRooster.ProviderConfig.Tests.TestSupport;

namespace PinkRooster.ProviderConfig.Jev.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AmbientEnvironmentCollection
{
    public const string Name = "Ambient environment";
}

[Collection(AmbientEnvironmentCollection.Name)]
public sealed class AmbientEnvironmentTests
{
    // TYPESAFE_LIVE_TESTS is deliberately not set here: the live test must not run because of this test's TYPESAFE_API_KEY.
    private static readonly string[] Variables = ["TYPESAFE_API_KEY", "TYPESAFE_BASE_URL", "TYPESAFE_DEFAULT_MODEL"];

    private static async Task WithAmbient(Func<Task> body)
    {
        Dictionary<string, string?> previous = Variables.ToDictionary(v => v, Environment.GetEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable("TYPESAFE_API_KEY", "ambient");
            Environment.SetEnvironmentVariable("TYPESAFE_BASE_URL", "http://ambient");
            Environment.SetEnvironmentVariable("TYPESAFE_DEFAULT_MODEL", "ambient-model");

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

    [Fact]
    public Task Jev_IgnoresAmbientEnvironment() => WithAmbient(async () =>
    {
        StubUpstream stub = new StubUpstream().RespondJson(JevResponses.Noul());
        ServiceCollection services = new();
        services.AddProviderConfig().AddJev().Configure(o => o.Providers["up"] = new ProviderOptions
        {
            Type = "jev",
            ApiKey = "k1",
            Endpoint = new Uri("http://up/prefix"),
        });
        services.AddSingleton(new ProviderHttpHandler(stub));
        using ServiceProvider provider = services.BuildServiceProvider();

        await provider.GetRequiredService<ISystemOneClientFactory>().GetSystemOneClient("up", "jev-1.14")
            .SystemOneAsync("state", new Dictionary<string, Question> { ["q"] = new Noul("Is it?") });

        Assert.Equal("up", stub.Requests[0].Uri.Host);
        Assert.Equal("Bearer k1", stub.Requests[0].Headers["Authorization"]);
        Assert.Equal("jev-1.14", (string?)stub.Requests[0].Body!["model"]);
    });
}
