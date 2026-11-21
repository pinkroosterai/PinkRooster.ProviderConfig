using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PinkRooster.ProviderConfig.Tests.TestSupport;

namespace PinkRooster.ProviderConfig.Tests.Options;

public sealed class ProviderOptionsBindingTests
{
    // These tests are about binding; the types they name are registered so the startup validation accepts them.
    private static ProviderConfigOptions Resolve(IServiceCollection services)
    {
        RegisterTypes(services);
        using ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<ProviderConfigOptions>>().Value;
    }

    private static ProviderConfigBuilder RegisterTypes(IServiceCollection services)
    {
        ProviderConfigBuilder builder = services.AddProviderConfig();
        foreach (string type in new[] { "t", "a", "b" })
        {
            builder.AddProviderType<IChatClient>(type, _ => id => new FakeChatClient(type, id));
        }

        builder.AddProviderType<IChatClient>("openai_responses", _ => id => new FakeChatClient("openai_responses", id), new ProviderTypeOptions { UsesStore = true });
        return builder.AddProviderType<IChatClient>("retry", _ => id => new FakeChatClient("retry", id), new ProviderTypeOptions { SupportsMaxRetries = true });
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
            new Dictionary<string, string?> { ["Providers:GPT:Type"] = "t", ["Providers:GPT:ApiKey"] = "json" },
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

    [Theory]
    [InlineData("", null)]
    [InlineData(", \"MaxRetries\": null", null)]
    [InlineData(", \"MaxRetries\": {}", null)]
    [InlineData(", \"MaxRetries\": 0", 0)]
    [InlineData(", \"MaxRetries\": 3", 3)]
    public async Task MaxRetries_BindsAtStartup(string setting, int? expected)
    {
        HostApplicationBuilder builder = HostWithRetryConfig(setting);
        using IHost host = builder.Build();

        await host.StartAsync();

        Assert.Equal(expected, host.Services.GetRequiredService<IOptions<ProviderConfigOptions>>().Value.Providers["indexing"].MaxRetries);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("\"\"")]
    [InlineData("\"  \"")]
    [InlineData("\"typo\"")]
    [InlineData("1.5")]
    [InlineData("2147483648")]
    [InlineData("[]")]
    [InlineData("[0]")]
    [InlineData("{ \"value\": 0 }")]
    public async Task InvalidMaxRetries_FailsAtStartup(string value)
    {
        HostApplicationBuilder builder = HostWithRetryConfig($", \"MaxRetries\": {value}");
        using IHost host = builder.Build();

        OptionsValidationException error = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains("Providers:indexing:MaxRetries", error.Message);
    }

    [Fact]
    public void ValidConfiguredMaxRetries_CanBeOverriddenInCode()
    {
        HostApplicationBuilder builder = HostWithRetryConfig(", \"MaxRetries\": 3");
        builder.Services.AddProviderConfig().Configure(options => options.Providers["indexing"].MaxRetries = 0);
        using IHost host = builder.Build();

        Assert.Equal(0, host.Services.GetRequiredService<IOptions<ProviderConfigOptions>>().Value.Providers["indexing"].MaxRetries);
    }

    [Fact]
    public void MalformedMaxRetries_FailsOptionsResolution_EvenWithCodeOverride()
    {
        ServiceCollection services = new();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Providers:indexing:Type"] = "retry",
            ["Providers:indexing:MaxRetries"] = "typo",
        }).Build());
        services.AddProviderConfig().Configure(options => options.Providers["indexing"].MaxRetries = 0);

        OptionsValidationException error = Assert.Throws<OptionsValidationException>(() => Resolve(services));

        Assert.Contains("Providers:indexing:MaxRetries", error.Message);
    }

    private static HostApplicationBuilder HostWithRetryConfig(string setting)
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        using var json = new MemoryStream(Encoding.UTF8.GetBytes($$$$"""{"Providers":{"indexing":{"Type":"retry"{{{{setting}}}}}}}"""));
        builder.Configuration.AddJsonStream(json);
        RegisterTypes(builder.Services);
        return builder;
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
