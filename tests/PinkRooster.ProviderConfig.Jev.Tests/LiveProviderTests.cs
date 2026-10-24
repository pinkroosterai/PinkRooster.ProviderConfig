using Jev.Net;
using Microsoft.Extensions.DependencyInjection;

namespace PinkRooster.ProviderConfig.Jev.Tests;

/// <summary>Calls the real TypeSafe API. Skipped unless <c>TYPESAFE_LIVE_TESTS=1</c> and <c>TYPESAFE_API_KEY</c> are set.</summary>
public sealed class LiveProviderTests
{
    [Fact]
    public async Task Jev_AnswersANoul()
    {
        string? key = Environment.GetEnvironmentVariable("TYPESAFE_API_KEY");
        if (Environment.GetEnvironmentVariable("TYPESAFE_LIVE_TESTS") != "1" || string.IsNullOrWhiteSpace(key))
        {
            Assert.Skip("Set TYPESAFE_LIVE_TESTS=1 and TYPESAFE_API_KEY to run this test.");
        }

        ServiceCollection services = new();
        services.AddProviderConfig().AddJev().Configure(o => o.Providers["live"] = new ProviderOptions { Type = "jev", ApiKey = key });
        using ServiceProvider provider = services.BuildServiceProvider();

        SystemOneResponse response = await provider.GetRequiredService<ISystemOneClientFactory>().GetSystemOneClient("live", "jev-1.14")
            .SystemOneAsync(
                "I was charged twice. Please refund the duplicate.",
                new Dictionary<string, Question> { ["refund"] = new Noul("Is the customer asking for money back?") });

        Assert.True(response.Answers.ContainsKey("refund"));
    }
}
