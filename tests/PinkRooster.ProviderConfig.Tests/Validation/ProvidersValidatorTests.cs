using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PinkRooster.ProviderConfig.Tests.TestSupport;

namespace PinkRooster.ProviderConfig.Tests.Validation;

public sealed class ProvidersValidatorTests
{
    private const string Known = "fake, needs_key";
    private const string Sentinel = "sentinel-key-123";

    private static ProviderConfigBuilder AddTypes(IServiceCollection services, bool needsKeyType = true)
    {
        ProviderConfigBuilder builder = services.AddProviderConfig()
            .AddProviderType<IChatClient>("fake", _ => id => new FakeChatClient("fake", id));
        if (needsKeyType)
        {
            builder.AddProviderType<IChatClient>(
                "needs_key",
                _ => id => new FakeChatClient("needs_key", id),
                new ProviderTypeOptions
                {
                    Validate = provider => string.IsNullOrWhiteSpace(provider.ApiKey) ? ["ApiKey is required for type 'needs_key'."] : [],
                    UsesStore = true,
                });
        }

        return builder;
    }

    private static ProviderConfigOptions Resolve(Action<ProviderConfigOptions> configure, bool needsKeyType = true)
    {
        ServiceCollection services = new();
        AddTypes(services, needsKeyType).Configure(configure);
        using ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<ProviderConfigOptions>>().Value;
    }

    private static string[] Failures(Action<ProviderConfigOptions> configure, bool needsKeyType = true) =>
        [.. Assert.Throws<OptionsValidationException>(() => Resolve(configure, needsKeyType)).Failures];

    public static TheoryData<string, ProviderOptions?, string> InvalidProviders => new()
    {
        { "bad id", new ProviderOptions { Type = "fake", ApiKey = Sentinel }, "Provider ID 'bad id' may only contain letters, digits, '_' and '-'." },
        { "p", new ProviderOptions { Type = null, ApiKey = Sentinel }, $"Providers:p:Type is required. Known types: {Known}." },
        { "p", new ProviderOptions { Type = " ", ApiKey = Sentinel }, $"Providers:p:Type is required. Known types: {Known}." },
        { "p", new ProviderOptions { Type = "nope", ApiKey = Sentinel }, $"Providers:p:Type 'nope' is not a known provider type. Known types: {Known}." },
        { "p", new ProviderOptions { Type = "fake", ApiKey = Sentinel, Endpoint = new Uri("/relative", UriKind.Relative) }, "Providers:p:Endpoint must be an absolute http or https URL." },
        { "p", new ProviderOptions { Type = "fake", ApiKey = Sentinel, Endpoint = new Uri("ftp://x/") }, "Providers:p:Endpoint must be an absolute http or https URL." },
        { "p", new ProviderOptions { Type = "fake", ApiKey = Sentinel, Timeout = TimeSpan.Zero }, "Providers:p:Timeout must be greater than zero." },
        { "p", new ProviderOptions { Type = "fake", ApiKey = Sentinel, Timeout = TimeSpan.FromSeconds(-1) }, "Providers:p:Timeout must be greater than zero." },
        { "p", new ProviderOptions { Type = "needs_key", ApiKey = " " }, "Providers:p:ApiKey is required for type 'needs_key'." },
        { "p", new ProviderOptions { Type = "fake", ApiKey = Sentinel, Store = true }, "Providers:p:Store applies only to type 'needs_key'." },
        { "p", null, "Providers:p is empty." },
    };

    [Theory]
    [MemberData(nameof(InvalidProviders))]
    public void Start_Fails_WithTheExpectedMessage(string id, ProviderOptions? provider, string expected)
    {
        string[] failures = Failures(options => options.Providers[id] = provider!);

        Assert.Contains(expected, failures);
    }

    [Fact]
    public void TheKey_NeverAppearsInValidationErrors()
    {
        foreach (var row in InvalidProviders)
        {
            (string id, ProviderOptions? provider, _) = row.Data;
            if (provider is { Type: "needs_key" })
            {
                provider.ApiKey = Sentinel;
                provider.Store = null;
                provider.Timeout = TimeSpan.Zero;
            }

            string[] failures = Failures(options => options.Providers[id] = provider!);

            Assert.All(failures, failure => Assert.DoesNotContain(Sentinel, failure));
        }
    }

    [Fact]
    public void Store_OnAnyType_WhenNoTypeUsesStore()
    {
        string[] failures = Failures(options => options.Providers["p"] = new ProviderOptions { Type = "fake", Store = true }, needsKeyType: false);

        Assert.Equal(["Providers:p:Store is not used by type 'fake'."], failures);
    }

    [Fact]
    public void Store_ListsEveryType_WhenSeveralUseStore()
    {
        ServiceCollection services = new();
        ProviderConfigBuilder builder = AddTypes(services);
        builder.AddProviderType<IChatClient>("also_store", _ => id => new FakeChatClient("also_store", id), new ProviderTypeOptions { UsesStore = true });
        builder.Configure(options => options.Providers["p"] = new ProviderOptions { Type = "fake", Store = true });
        using ServiceProvider provider = services.BuildServiceProvider();

        OptionsValidationException e = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<ProviderConfigOptions>>().Value);

        Assert.Equal(["Providers:p:Store applies only to types 'also_store', 'needs_key'."], e.Failures);
    }

    [Fact]
    public void Start_ReportsEveryFailure_AtOnce()
    {
        string[] failures = Failures(options =>
        {
            options.Providers["bad id"] = new ProviderOptions { Type = "nope" };
            options.Providers["p"] = new ProviderOptions { Type = "needs_key", Timeout = TimeSpan.Zero };
        });

        Assert.Equal(4, failures.Length);
        Assert.Contains("Provider ID 'bad id' may only contain letters, digits, '_' and '-'.", failures);
        Assert.Contains($"Providers:bad id:Type 'nope' is not a known provider type. Known types: {Known}.", failures);
        Assert.Contains("Providers:p:ApiKey is required for type 'needs_key'.", failures);
        Assert.Contains("Providers:p:Timeout must be greater than zero.", failures);
    }

    [Fact]
    public void Start_Succeeds_ForProvidersSetInCode()
    {
        ProviderConfigOptions options = Resolve(o => o.Providers["p"] = new ProviderOptions { Type = "needs_key", ApiKey = "k", Store = true });

        Assert.Equal("needs_key", options.Providers["p"].Type);
    }

    [Fact]
    public void Start_Succeeds_ForACustomType_WithoutAKey()
    {
        ProviderConfigOptions options = Resolve(o => o.Providers["p"] = new ProviderOptions { Type = "fake" });

        Assert.Single(options.Providers);
    }

    [Fact]
    public void Start_Succeeds_WithNoProviders()
    {
        Assert.Empty(Resolve(_ => { }).Providers);
    }

    [Fact]
    public async Task Start_Fails_ForAnInvalidProvider()
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        AddTypes(builder.Services).Configure(o => o.Providers["p"] = new ProviderOptions { Type = "nope" });
        using IHost host = builder.Build();

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }
}
