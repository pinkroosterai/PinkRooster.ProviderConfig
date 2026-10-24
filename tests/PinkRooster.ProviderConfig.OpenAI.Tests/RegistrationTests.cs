using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PinkRooster.ProviderConfig.OpenAI.Tests;

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
    public void AddOpenAI_RegistersBothTypes()
    {
        using ServiceProvider provider = Build(builder => builder.AddOpenAI().Configure(options =>
        {
            options.Providers["a"] = new ProviderOptions { Type = "openai", ApiKey = "k" };
            options.Providers["b"] = new ProviderOptions { Type = "openai_responses", ApiKey = "k", Store = true };
        }));

        ProviderConfigOptions options = Options(provider);

        Assert.Equal(2, options.Providers.Count);
        Assert.IsAssignableFrom<IChatClient>(provider.GetRequiredService<IChatClientFactory>().GetChatClient("a", "m"));
        Assert.IsAssignableFrom<IChatClient>(provider.GetRequiredService<IChatClientFactory>().GetChatClient("b", "m"));
    }

    [Fact]
    public void AddOpenAI_Twice_IsANoOp()
    {
        using ServiceProvider provider = Build(builder => builder.AddOpenAI().AddOpenAI().Configure(options =>
            options.Providers["a"] = new ProviderOptions { Type = "openai", ApiKey = "k" }));

        Assert.Single(Options(provider).Providers);
    }

    [Theory]
    [InlineData("openai")]
    [InlineData("openai_responses")]
    public void OpenAI_ApiKeyRequired_WithoutAnEndpoint(string type)
    {
        using ServiceProvider provider = Build(builder => builder.AddOpenAI().Configure(options =>
            options.Providers["p"] = new ProviderOptions { Type = type }));

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => Options(provider));

        Assert.Contains($"Providers:p:ApiKey is required for type '{type}' without an Endpoint.", exception.Failures);
    }

    [Fact]
    public void Store_IsRejected_OnOpenAIChatCompletions()
    {
        using ServiceProvider provider = Build(builder => builder.AddOpenAI().Configure(options =>
            options.Providers["p"] = new ProviderOptions { Type = "openai", ApiKey = "k", Store = true }));

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() => Options(provider));

        Assert.Contains("Providers:p:Store applies only to type 'openai_responses'.", exception.Failures);
    }
}
