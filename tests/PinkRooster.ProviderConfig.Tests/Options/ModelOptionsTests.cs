using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PinkRooster.ProviderConfig.Tests.TestSupport;

namespace PinkRooster.ProviderConfig.Tests.Options;

public sealed class ModelOptionsTests
{
    [Fact]
    public async Task Models_JsonAdditionalProperties_ReachClientAsPrimitives()
    {
        const string json = """
            { "Providers": { "p": { "Type": "fake", "Models": { "m": {
              "AdditionalProperties": { "budget": 2048, "strict": true, "disabled": false,
                "ratio": 0.25, "tier": "gold", "empty": "", "nothing": null }
            } } } } }
            """;
        using MemoryStream stream = new(System.Text.Encoding.UTF8.GetBytes(json));
        IConfiguration configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();
        RecordingChatClient inner = new();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddProviderConfig()
            .AddProviderType<IChatClient>("fake", _ => _ => inner)
            .Configure(options => options.Providers["p"].Models["m"].AdditionalProperties["literal"] = "2048");
        using ServiceProvider provider = services.BuildServiceProvider();
        IChatClient client = provider.GetRequiredService<IChatClientFactory>().GetChatClient("p", "m");

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hello")]);

        Assert.NotNull(inner.LastOptions?.AdditionalProperties);
        AdditionalPropertiesDictionary properties = inner.LastOptions.AdditionalProperties;
        Assert.Equal(2048L, Assert.IsType<long>(properties["budget"]));
        Assert.True(Assert.IsType<bool>(properties["strict"]));
        Assert.False(Assert.IsType<bool>(properties["disabled"]));
        Assert.Equal(0.25, Assert.IsType<double>(properties["ratio"]));
        Assert.Equal("gold", properties["tier"]);
        Assert.Equal("", properties["empty"]);
        Assert.Null(properties["nothing"]);
        Assert.Equal("2048", Assert.IsType<string>(properties["literal"]));
    }

    [Fact]
    public void Models_BindsFromConfiguration_KeyedByModelId()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Providers:openai:Type"] = "fake",
                ["Providers:openai:Models:gpt-5:Temperature"] = "0.2",
                ["Providers:openai:Models:gpt-5:ReasoningEffort"] = "High",
                ["Providers:openai:Models:gpt-5:MaxOutputTokens"] = "4096",
                ["Providers:openai:Models:gpt-5:TopP"] = "0.95",
                ["Providers:openai:Models:gpt-5:TopK"] = "40",
                ["Providers:openai:Models:gpt-5:FrequencyPenalty"] = "0.5",
                ["Providers:openai:Models:gpt-5:PresencePenalty"] = "0.5",
                ["Providers:openai:Models:gpt-5:Seed"] = "12345",
                ["Providers:openai:Models:gpt-5:StopSequences:0"] = "###",
                ["Providers:openai:Models:gpt-5:AdditionalProperties:custom_budget"] = "2048",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddProviderConfig()
            .AddProviderType<IChatClient>("fake", _ => id => new FakeChatClient("fake", id));

        using ServiceProvider sp = services.BuildServiceProvider();
        ProviderConfigOptions options = sp.GetRequiredService<IOptions<ProviderConfigOptions>>().Value;

        Assert.True(options.Providers["openai"].Models.TryGetValue("gpt-5", out ModelOptions? model));
        Assert.NotNull(model);
        Assert.Equal(0.2f, model.Temperature);
        Assert.Equal(ReasoningEffort.High, model.ReasoningEffort);
        Assert.Equal(4096, model.MaxOutputTokens);
        Assert.Equal(0.95f, model.TopP);
        Assert.Equal(40, model.TopK);
        Assert.Equal(0.5f, model.FrequencyPenalty);
        Assert.Equal(0.5f, model.PresencePenalty);
        Assert.Equal(12345L, model.Seed);
        Assert.Equal(["###"], model.StopSequences);
        Assert.Equal("2048", model.AdditionalProperties["custom_budget"]?.ToString());
    }

    [Fact]
    public void Models_LookupIsCaseInsensitive()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Providers:openai:Type"] = "fake",
                ["Providers:openai:Models:GPT-5-Mini:Temperature"] = "0.7",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddProviderConfig()
            .AddProviderType<IChatClient>("fake", _ => id => new FakeChatClient("fake", id));

        using ServiceProvider sp = services.BuildServiceProvider();
        ProviderConfigOptions options = sp.GetRequiredService<IOptions<ProviderConfigOptions>>().Value;

        Assert.True(options.Providers["openai"].Models.TryGetValue("gpt-5-mini", out ModelOptions? model));
        Assert.NotNull(model);
        Assert.Equal(0.7f, model.Temperature);
    }
}
