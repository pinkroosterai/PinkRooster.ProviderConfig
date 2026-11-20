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
    public void AddOpenAI_RegistersAllTypes()
    {
        using ServiceProvider provider = Build(builder => builder.AddOpenAI().Configure(options =>
        {
            options.Providers["a"] = new ProviderOptions { Type = "openai", ApiKey = "k" };
            options.Providers["b"] = new ProviderOptions { Type = "openai_responses", ApiKey = "k", Store = true };
            options.Providers["c"] = new ProviderOptions { Type = "openai_embedding", ApiKey = "k" };
            options.Providers["d"] = new ProviderOptions { Type = "openai_embeddings", ApiKey = "k" };
        }));

        ProviderConfigOptions options = Options(provider);

        Assert.Equal(4, options.Providers.Count);
        Assert.IsAssignableFrom<IChatClient>(provider.GetRequiredService<IChatClientFactory>().GetChatClient("a", "m"));
        Assert.IsAssignableFrom<IChatClient>(provider.GetRequiredService<IChatClientFactory>().GetChatClient("b", "m"));
        Assert.IsAssignableFrom<IEmbeddingGenerator<string, Embedding<float>>>(
            provider.GetRequiredService<IEmbeddingGeneratorFactory>().GetEmbeddingGenerator("c", "m"));
        Assert.IsAssignableFrom<IEmbeddingGenerator<string, Embedding<float>>>(
            provider.GetRequiredService<IEmbeddingGeneratorFactory>().GetEmbeddingGenerator("d", "m"));
    }

    [Fact]
    public void TypeMismatch_ThrowsInformativeException()
    {
        using ServiceProvider provider = Build(builder => builder.AddOpenAI().Configure(options =>
        {
            options.Providers["chat"] = new ProviderOptions { Type = "openai", ApiKey = "k" };
            options.Providers["embed"] = new ProviderOptions { Type = "openai_embedding", ApiKey = "k" };
        }));

        InvalidOperationException exChat = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IChatClientFactory>().GetChatClient("embed", "m"));
        Assert.Contains("creates IEmbeddingGenerator`2, not IChatClient", exChat.Message);

        InvalidOperationException exEmbed = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IEmbeddingGeneratorFactory>().GetEmbeddingGenerator("chat", "m"));
        Assert.Contains("creates IChatClient, not IEmbeddingGenerator`2", exEmbed.Message);
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
    [InlineData("openai_embedding")]
    [InlineData("openai_embeddings")]
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
