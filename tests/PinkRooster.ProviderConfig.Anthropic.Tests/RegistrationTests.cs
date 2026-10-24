using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PinkRooster.ProviderConfig.Anthropic.Tests;

public sealed class RegistrationTests
{
    private static ServiceProvider Build(Action<ProviderConfigBuilder> configure)
    {
        ServiceCollection services = new();
        configure(services.AddProviderConfig());
        return services.BuildServiceProvider();
    }

    private static ProviderConfigOptions Options(ServiceProvider provider) => provider.GetRequiredService<IOptions<ProviderConfigOptions>>().Value;

    [Fact]
    public void AddAnthropic_Twice_IsANoOp()
    {
        using ServiceProvider provider = Build(builder => builder.AddAnthropic().AddAnthropic().Configure(options =>
            options.Providers["a"] = new ProviderOptions { Type = "anthropic", ApiKey = "k" }));

        Assert.Single(Options(provider).Providers);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Anthropic_ApiKeyRequired(string? apiKey)
    {
        using ServiceProvider provider = Build(builder => builder.AddAnthropic().Configure(options =>
            options.Providers["p"] = new ProviderOptions { Type = "anthropic", ApiKey = apiKey }));

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => Options(provider));

        Assert.Contains("Providers:p:ApiKey is required for type 'anthropic'.", exception.Failures);
    }
}
