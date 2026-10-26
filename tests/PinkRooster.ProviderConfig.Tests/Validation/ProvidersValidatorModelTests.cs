using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PinkRooster.ProviderConfig.Tests.TestSupport;

namespace PinkRooster.ProviderConfig.Tests.Validation;

public sealed class ProvidersValidatorModelTests
{
    private static ProviderConfigOptions Resolve(Action<ProviderConfigOptions> configure)
    {
        ServiceCollection services = new();
        services.AddProviderConfig()
            .AddProviderType<IChatClient>("fake", _ => id => new FakeChatClient("fake", id))
            .Configure(configure);

        using ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<ProviderConfigOptions>>().Value;
    }

    private static string[] Failures(Action<ProviderConfigOptions> configure) =>
        [.. Assert.Throws<OptionsValidationException>(() => Resolve(configure)).Failures];

    public static TheoryData<string, ModelOptions, string> InvalidModelOptions => new()
    {
        { "bad model id!", new ModelOptions(), "Providers:p:Model ID 'bad model id!' may only contain letters, digits, '_', '-' and '.'." },
        { "", new ModelOptions(), "Providers:p:Model ID cannot be empty." },
        { "m", new ModelOptions { Temperature = -0.1f }, "Providers:p:Models:m:Temperature must be between 0.0 and 2.0." },
        { "m", new ModelOptions { Temperature = 2.1f }, "Providers:p:Models:m:Temperature must be between 0.0 and 2.0." },
        { "m", new ModelOptions { TopP = -0.01f }, "Providers:p:Models:m:TopP must be between 0.0 and 1.0." },
        { "m", new ModelOptions { TopP = 1.01f }, "Providers:p:Models:m:TopP must be between 0.0 and 1.0." },
        { "m", new ModelOptions { FrequencyPenalty = -2.1f }, "Providers:p:Models:m:FrequencyPenalty must be between -2.0 and 2.0." },
        { "m", new ModelOptions { FrequencyPenalty = 2.1f }, "Providers:p:Models:m:FrequencyPenalty must be between -2.0 and 2.0." },
        { "m", new ModelOptions { PresencePenalty = -2.1f }, "Providers:p:Models:m:PresencePenalty must be between -2.0 and 2.0." },
        { "m", new ModelOptions { PresencePenalty = 2.1f }, "Providers:p:Models:m:PresencePenalty must be between -2.0 and 2.0." },
        { "m", new ModelOptions { MaxOutputTokens = 0 }, "Providers:p:Models:m:MaxOutputTokens must be greater than zero." },
        { "m", new ModelOptions { MaxOutputTokens = -5 }, "Providers:p:Models:m:MaxOutputTokens must be greater than zero." },
        { "m", new ModelOptions { TopK = 0 }, "Providers:p:Models:m:TopK must be greater than zero." },
        { "m", new ModelOptions { TopK = -10 }, "Providers:p:Models:m:TopK must be greater than zero." },
        { "m", new ModelOptions { ReasoningEffort = (ReasoningEffort)999 }, "Providers:p:Models:m:ReasoningEffort value 999 is invalid." },
    };

    [Theory]
    [MemberData(nameof(InvalidModelOptions))]
    public void ModelValidation_ReportsExpectedFailure(string modelId, ModelOptions model, string expected)
    {
        string[] failures = Failures(options =>
        {
            var provider = new ProviderOptions { Type = "fake" };
            provider.Models[modelId] = model;
            options.Providers["p"] = provider;
        });

        Assert.Contains(expected, failures);
    }

    [Fact]
    public void ModelValidation_PassesForValidModel()
    {
        ProviderConfigOptions options = Resolve(opts =>
        {
            var provider = new ProviderOptions { Type = "fake" };
            provider.Models["gpt-5.mini"] = new ModelOptions
            {
                Temperature = 0.5f,
                TopP = 0.9f,
                FrequencyPenalty = 0.0f,
                PresencePenalty = 0.0f,
                MaxOutputTokens = 1000,
                TopK = 50,
                ReasoningEffort = ReasoningEffort.Medium,
            };
            opts.Providers["p"] = provider;
        });

        Assert.Single(options.Providers["p"].Models);
    }
}
