using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PinkRooster.ProviderConfig.Tests.TestSupport;

namespace PinkRooster.ProviderConfig.Tests.Validation;

public sealed class PlainHttpWarningTests
{
    private static ServiceCollection Build(string endpoint, string? apiKey, CapturingLoggerProvider? logs)
    {
        ServiceCollection services = new();
        if (logs is not null)
        {
            services.AddLogging(logging => logging.AddProvider(logs));
        }

        services.AddProviderConfig()
            .AddProviderType<IChatClient>("fake", _ => id => new FakeChatClient("fake", id))
            .Configure(options => options.Providers["remote"] = new ProviderOptions { Type = "fake", ApiKey = apiKey, Endpoint = new Uri(endpoint) });
        return services;
    }

    private static List<CapturingLoggerProvider.LogEntry> Warnings(string endpoint, string? apiKey)
    {
        CapturingLoggerProvider logs = new();
        using ServiceProvider provider = Build(endpoint, apiKey, logs).BuildServiceProvider();
        _ = provider.GetRequiredService<IOptions<ProviderConfigOptions>>().Value;
        return [.. logs.Entries.Where(entry => entry.Level == LogLevel.Warning)];
    }

    [Fact]
    public void PlainHttpToARemoteHost_LogsOneWarning()
    {
        var warnings = Warnings("http://10.0.0.5/v1", "any-key");

        var warning = Assert.Single(warnings);
        Assert.Equal("Upstream provider 'remote' sends its API key over plain HTTP to 10.0.0.5. Use https unless the network is trusted.", warning.Message);
    }

    [Theory]
    [InlineData("http://127.0.0.1:11434/v1")]
    [InlineData("http://localhost/v1")]
    [InlineData("https://api.example.com/v1")]
    public void PlainHttpToLoopbackOrHttps_LogsNothing(string endpoint)
    {
        Assert.Empty(Warnings(endpoint, "any-key"));
    }

    [Fact]
    public void PlainHttp_WithoutAKey_LogsNothing()
    {
        Assert.Empty(Warnings("http://10.0.0.5/v1", null));
    }

    [Fact]
    public void PlainHttpWarning_IsLoggedOnce_AcrossRepeatedReads()
    {
        CapturingLoggerProvider logs = new();
        using ServiceProvider provider = Build("http://10.0.0.5/v1", "any-key", logs).BuildServiceProvider();

        _ = provider.GetRequiredService<IOptions<ProviderConfigOptions>>().Value;
        _ = provider.GetRequiredService<IOptionsMonitor<ProviderConfigOptions>>().CurrentValue;
        for (int i = 0; i < 2; i++)
        {
            using IServiceScope scope = provider.CreateScope();
            _ = scope.ServiceProvider.GetRequiredService<IOptionsSnapshot<ProviderConfigOptions>>().Value;
        }

        Assert.Single(logs.Entries, entry => entry.Level == LogLevel.Warning);
    }

    [Fact]
    public void NoLoggerFactory_DoesNotThrow()
    {
        using ServiceProvider provider = Build("http://10.0.0.5/v1", "any-key", logs: null).BuildServiceProvider();

        ProviderConfigOptions options = provider.GetRequiredService<IOptions<ProviderConfigOptions>>().Value;

        Assert.Single(options.Providers);
    }
}
