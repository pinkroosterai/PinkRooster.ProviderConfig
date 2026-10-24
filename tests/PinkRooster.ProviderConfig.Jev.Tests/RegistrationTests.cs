using Jev.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PinkRooster.ProviderConfig.Tests.TestSupport;

namespace PinkRooster.ProviderConfig.Jev.Tests;

public sealed class RegistrationTests
{
    private static ServiceProvider Build(Action<ProviderConfigBuilder> configure, CapturingLoggerProvider? logs = null)
    {
        ServiceCollection services = new();
        if (logs is not null)
        {
            services.AddLogging(logging => logging.AddProvider(logs));
        }

        configure(services.AddProviderConfig());
        return services.BuildServiceProvider();
    }

    private static ProviderConfigOptions Options(ServiceProvider provider) => provider.GetRequiredService<IOptions<ProviderConfigOptions>>().Value;

    [Fact]
    public void AddJev_Twice_IsANoOp()
    {
        using ServiceProvider provider = Build(builder => builder.AddJev().AddJev().Configure(options =>
            options.Providers["j"] = new ProviderOptions { Type = "jev", ApiKey = "k" }));

        Assert.Single(Options(provider).Providers);
        Assert.Single(provider.GetServices<ISystemOneClientFactory>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Jev_ApiKeyRequired(string? apiKey)
    {
        using ServiceProvider provider = Build(builder => builder.AddJev().Configure(options =>
            options.Providers["p"] = new ProviderOptions { Type = "jev", ApiKey = apiKey }));

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => Options(provider));

        Assert.Contains("Providers:p:ApiKey is required for type 'jev'.", exception.Failures);
    }

    [Fact]
    public void Store_IsRejected_OnJev()
    {
        using ServiceProvider provider = Build(builder => builder.AddJev().Configure(options =>
            options.Providers["p"] = new ProviderOptions { Type = "jev", ApiKey = "k", Store = true }));

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => Options(provider));

        Assert.Contains("Providers:p:Store is not used by type 'jev'.", exception.Failures);
    }

    [Fact]
    public void PlainHttpToARemoteHost_Warns_ForJev()
    {
        CapturingLoggerProvider logs = new();
        using ServiceProvider provider = Build(builder => builder.AddJev().Configure(options =>
            options.Providers["remote"] = new ProviderOptions { Type = "jev", ApiKey = "k", Endpoint = new Uri("http://10.0.0.5/prefix") }), logs);

        _ = Options(provider);

        CapturingLoggerProvider.LogEntry warning = Assert.Single(logs.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Equal("Upstream provider 'remote' sends its API key over plain HTTP to 10.0.0.5. Use https unless the network is trusted.", warning.Message);
    }
}
