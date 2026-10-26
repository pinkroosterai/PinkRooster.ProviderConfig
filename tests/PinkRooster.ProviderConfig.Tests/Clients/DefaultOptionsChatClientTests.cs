using Microsoft.Extensions.AI;
using PinkRooster.ProviderConfig.Tests.TestSupport;

namespace PinkRooster.ProviderConfig.Tests.Clients;

public sealed class DefaultOptionsChatClientTests
{
    private static (DefaultOptionsChatClient client, RecordingChatClient inner) CreateClient(
        string defaultModelId,
        Dictionary<string, ModelOptions> models)
    {
        var inner = new RecordingChatClient();
        var client = new DefaultOptionsChatClient(inner, defaultModelId, models);
        return (client, inner);
    }

    [Fact]
    public async Task GetResponseAsync_WhenOptionsNull_AppliesAllDefaults()
    {
        var models = new Dictionary<string, ModelOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-5"] = new ModelOptions
            {
                Temperature = 0.2f,
                MaxOutputTokens = 1000,
                ReasoningEffort = ReasoningEffort.High,
                StopSequences = ["###"],
                AdditionalProperties = new Dictionary<string, object?> { ["tier"] = "gold" }
            }
        };

        var (client, inner) = CreateClient("gpt-5", models);
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.NotNull(inner.LastOptions);
        Assert.Equal(0.2f, inner.LastOptions.Temperature);
        Assert.Equal(1000, inner.LastOptions.MaxOutputTokens);
        Assert.Equal(ReasoningEffort.High, inner.LastOptions.Reasoning?.Effort);
        Assert.Equal(["###"], inner.LastOptions.StopSequences);
        Assert.Equal("gold", inner.LastOptions.AdditionalProperties?["tier"]);
    }

    [Fact]
    public async Task GetResponseAsync_ExplicitZeroTemperature_IsNotReplaced()
    {
        var models = new Dictionary<string, ModelOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-5"] = new ModelOptions { Temperature = 0.7f }
        };

        var (client, inner) = CreateClient("gpt-5", models);
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], new ChatOptions { Temperature = 0.0f });

        Assert.NotNull(inner.LastOptions);
        Assert.Equal(0.0f, inner.LastOptions.Temperature);
    }

    [Fact]
    public async Task GetResponseAsync_CallerOptions_AreNotMutated()
    {
        var models = new Dictionary<string, ModelOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-5"] = new ModelOptions { Temperature = 0.5f, MaxOutputTokens = 200 }
        };

        var callerOptions = new ChatOptions { Temperature = 0.8f };
        var (client, _) = CreateClient("gpt-5", models);
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], callerOptions);

        Assert.Equal(0.8f, callerOptions.Temperature);
        Assert.Null(callerOptions.MaxOutputTokens);
    }

    [Fact]
    public async Task GetResponseAsync_AdditionalProperties_KeyLevelMerge()
    {
        var models = new Dictionary<string, ModelOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-5"] = new ModelOptions
            {
                AdditionalProperties = new Dictionary<string, object?>
                {
                    ["retained_default"] = 123,
                    ["overridden_key"] = "config_value"
                }
            }
        };

        var callerOptions = new ChatOptions
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                ["overridden_key"] = "caller_value",
                ["caller_unique"] = "unique"
            }
        };

        var (client, inner) = CreateClient("gpt-5", models);
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], callerOptions);

        Assert.NotNull(inner.LastOptions?.AdditionalProperties);
        Assert.Equal(123, inner.LastOptions.AdditionalProperties["retained_default"]);
        Assert.Equal("caller_value", inner.LastOptions.AdditionalProperties["overridden_key"]);
        Assert.Equal("unique", inner.LastOptions.AdditionalProperties["caller_unique"]);
    }

    [Fact]
    public async Task GetResponseAsync_DynamicModelSwitch_UsesSwitchedModelDefaults()
    {
        var models = new Dictionary<string, ModelOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-5"] = new ModelOptions { Temperature = 0.2f },
            ["gpt-5-mini"] = new ModelOptions { Temperature = 0.7f }
        };

        var (client, inner) = CreateClient("gpt-5", models);
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], new ChatOptions { ModelId = "GPT-5-MINI" });

        Assert.NotNull(inner.LastOptions);
        Assert.Equal(0.7f, inner.LastOptions.Temperature);
    }

    [Fact]
    public async Task GetResponseAsync_UnlistedModel_BypassesDefaults()
    {
        var models = new Dictionary<string, ModelOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-5"] = new ModelOptions { Temperature = 0.2f }
        };

        var callerOptions = new ChatOptions { ModelId = "unlisted" };
        var (client, inner) = CreateClient("gpt-5", models);
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], callerOptions);

        Assert.Same(callerOptions, inner.LastOptions);
        Assert.Null(inner.LastOptions?.Temperature);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_AppliesDefaultsIdentically()
    {
        var models = new Dictionary<string, ModelOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-5"] = new ModelOptions { Temperature = 0.3f }
        };

        var (client, inner) = CreateClient("gpt-5", models);
        await foreach (var _ in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")]))
        {
        }

        Assert.NotNull(inner.LastOptions);
        Assert.Equal(0.3f, inner.LastOptions.Temperature);
    }

    [Fact]
    public async Task GetResponseAsync_AllScalarProperties_InjectedWhenCallerNull()
    {
        var models = new Dictionary<string, ModelOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-5"] = new ModelOptions
            {
                TopP = 0.9f,
                TopK = 40,
                FrequencyPenalty = 0.5f,
                PresencePenalty = 0.6f,
                Seed = 42L
            }
        };

        var (client, inner) = CreateClient("gpt-5", models);
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Assert.NotNull(inner.LastOptions);
        Assert.Equal(0.9f, inner.LastOptions.TopP);
        Assert.Equal(40, inner.LastOptions.TopK);
        Assert.Equal(0.5f, inner.LastOptions.FrequencyPenalty);
        Assert.Equal(0.6f, inner.LastOptions.PresencePenalty);
        Assert.Equal(42L, inner.LastOptions.Seed);
    }

    [Fact]
    public async Task GetResponseAsync_CallerScalarProperties_PreservedOverDefaults()
    {
        var models = new Dictionary<string, ModelOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-5"] = new ModelOptions
            {
                TopP = 0.9f,
                TopK = 40,
                FrequencyPenalty = 0.5f,
                PresencePenalty = 0.6f,
                Seed = 42L
            }
        };

        var callerOptions = new ChatOptions
        {
            TopP = 0.1f,
            TopK = 10,
            FrequencyPenalty = -0.5f,
            PresencePenalty = -0.6f,
            Seed = 99L
        };

        var (client, inner) = CreateClient("gpt-5", models);
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], callerOptions);

        Assert.NotNull(inner.LastOptions);
        Assert.Equal(0.1f, inner.LastOptions.TopP);
        Assert.Equal(10, inner.LastOptions.TopK);
        Assert.Equal(-0.5f, inner.LastOptions.FrequencyPenalty);
        Assert.Equal(-0.6f, inner.LastOptions.PresencePenalty);
        Assert.Equal(99L, inner.LastOptions.Seed);
    }

    [Fact]
    public async Task GetResponseAsync_CallerReasoningEffort_PreservedOverDefault()
    {
        var models = new Dictionary<string, ModelOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-5"] = new ModelOptions { ReasoningEffort = ReasoningEffort.High }
        };

        var callerOptions = new ChatOptions
        {
            Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Low }
        };

        var (client, inner) = CreateClient("gpt-5", models);
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], callerOptions);

        Assert.NotNull(inner.LastOptions?.Reasoning);
        Assert.Equal(ReasoningEffort.Low, inner.LastOptions.Reasoning.Effort);
    }

    [Fact]
    public async Task GetResponseAsync_CallerReasoningWithoutEffort_GetsDefaultEffort()
    {
        var models = new Dictionary<string, ModelOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-5"] = new ModelOptions { ReasoningEffort = ReasoningEffort.Medium }
        };

        var callerOptions = new ChatOptions
        {
            Reasoning = new ReasoningOptions()
        };

        var (client, inner) = CreateClient("gpt-5", models);
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], callerOptions);

        Assert.NotNull(inner.LastOptions?.Reasoning);
        Assert.Equal(ReasoningEffort.Medium, inner.LastOptions.Reasoning.Effort);
    }

    [Fact]
    public async Task GetResponseAsync_CallerStopSequences_NonEmpty_PreservedOverDefault()
    {
        var models = new Dictionary<string, ModelOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-5"] = new ModelOptions { StopSequences = ["default_stop"] }
        };

        var callerOptions = new ChatOptions { StopSequences = ["caller_stop"] };

        var (client, inner) = CreateClient("gpt-5", models);
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], callerOptions);

        Assert.NotNull(inner.LastOptions?.StopSequences);
        Assert.Equal(["caller_stop"], inner.LastOptions.StopSequences);
    }

    [Fact]
    public async Task GetResponseAsync_CallerStopSequences_Empty_ReplacedByDefault()
    {
        var models = new Dictionary<string, ModelOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-5"] = new ModelOptions { StopSequences = ["default_stop"] }
        };

        var callerOptions = new ChatOptions { StopSequences = [] };

        var (client, inner) = CreateClient("gpt-5", models);
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], callerOptions);

        Assert.NotNull(inner.LastOptions?.StopSequences);
        Assert.Equal(["default_stop"], inner.LastOptions.StopSequences);
    }

    [Fact]
    public async Task GetResponseAsync_WhenOptionsNullAndModelUnlisted_PassesNullOptions()
    {
        var models = new Dictionary<string, ModelOptions>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-5"] = new ModelOptions { Temperature = 0.5f }
        };

        var (client, inner) = CreateClient("unlisted-model", models);
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], options: null);

        Assert.Null(inner.LastOptions);
    }
}
