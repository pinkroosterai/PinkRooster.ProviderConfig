using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PinkRooster.ProviderConfig.Tests.Options;

public sealed class ProviderOptionsBindingTests
{
    private static ProviderConfigOptions Resolve(IServiceCollection services)
    {
        using ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<ProviderConfigOptions>>().Value;
    }

    private static ProviderConfigOptions Bind(params Dictionary<string, string?>[] sources)
    {
        var builder = new ConfigurationBuilder();
        foreach (var source in sources)
        {
            builder.AddInMemoryCollection(source);
        }

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(builder.Build());
        services.AddProviderConfig();
        return Resolve(services);
    }

    [Fact]
    public void Providers_BindFromTheSection_KeyedById()
    {
        var options = Bind(new Dictionary<string, string?>
        {
            ["Providers:gpt:Type"] = "openai_responses",
            ["Providers:gpt:Endpoint"] = "https://x/openai/v1",
            ["Providers:gpt:Timeout"] = "00:02:00",
            ["Providers:gpt:Store"] = "true",
        });

        var provider = options.Providers["gpt"];
        Assert.Equal("openai_responses", provider.Type);
        Assert.Equal(new Uri("https://x/openai/v1"), provider.Endpoint);
        Assert.Equal(TimeSpan.FromMinutes(2), provider.Timeout);
        Assert.True(provider.Store);
    }

    [Fact]
    public void Providers_IgnoreCase_ForTheId()
    {
        var options = Bind(new Dictionary<string, string?> { ["Providers:GPT:Type"] = "openai_responses" });

        Assert.Equal("openai_responses", options.Providers["gpt"].Type);
    }

    [Fact]
    public void Providers_LaterSourceOverridesTheKey()
    {
        var options = Bind(
            new Dictionary<string, string?> { ["Providers:GPT:ApiKey"] = "json" },
            new Dictionary<string, string?> { ["Providers:gpt:ApiKey"] = "env" });

        Assert.Single(options.Providers);
        Assert.Equal("env", options.Providers["gpt"].ApiKey);
    }

    [Fact]
    public void ProviderOptions_ToString_DoesNotShowTheKey()
    {
        var options = new ProviderOptions { ApiKey = "sentinel-key" };

        Assert.DoesNotContain("sentinel-key", options.ToString());
    }

    [Fact]
    public void Providers_FromCodeOnly_WithoutAnIConfiguration()
    {
        var services = new ServiceCollection();
        services.AddProviderConfig().Configure(o => o.Providers["p"] = new ProviderOptions { Type = "t" });

        Assert.Equal("t", Resolve(services).Providers["P"].Type);
    }

    [Fact]
    public void Code_OverridesConfiguration_ForTheSameId()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Providers:p:Type"] = "a" })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddProviderConfig().Configure(o => o.Providers["p"] = new ProviderOptions { Type = "b" });

        Assert.Equal("b", Resolve(services).Providers["p"].Type);
    }

    [Fact]
    public void AddProviderConfig_Twice_BindsOnce()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Providers:p:Type"] = "a" })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddProviderConfig();
        services.AddProviderConfig();

        Assert.Single(Resolve(services).Providers);
        Assert.Single(
            services,
            d => d.ServiceType == typeof(IConfigureOptions<ProviderConfigOptions>) && d.ImplementationType == typeof(ConfigurationBinding));
    }
}
