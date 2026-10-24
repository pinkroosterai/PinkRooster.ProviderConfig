using Anthropic;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace PinkRooster.ProviderConfig.Anthropic;

/// <summary>The built-in <c>anthropic</c> provider type: the Messages API of Anthropic or a compatible service.</summary>
internal static class AnthropicProviderType
{
    private const string DefaultEndpoint = "https://api.anthropic.com";

    /// <summary>
    /// Creates the client function of an <c>anthropic</c> provider: one SDK client, a chat client per model. The key and base URL are always
    /// set, so no <c>ANTHROPIC_*</c> environment variable can redirect the client.
    /// </summary>
    /// <param name="context">The provider's ID, settings and the application services.</param>
    /// <returns>The function that creates a chat client for a model ID.</returns>
    public static Func<string, IChatClient> Create(ProviderContext context)
    {
        ProviderOptions options = context.Options;
        string apiKey = options.ApiKey ?? string.Empty;
        string baseUrl = options.Endpoint?.ToString().TrimEnd('/') ?? DefaultEndpoint;

        // HttpClient is init-only and non-nullable, so it is set only through its own initializer when the test seam is present.
        ProviderHttpHandler? seam = context.Services.GetService<ProviderHttpHandler>();
        AnthropicClient client;
        if (seam is null)
        {
            client = new AnthropicClient { ApiKey = apiKey, BaseUrl = baseUrl, Timeout = options.Timeout };
        }
        else
        {
            // The seam client wraps a shared handler with disposeHandler: false, so disposing it leaves the handler alone.
            HttpClient http = seam.CreateClient();
            context.RegisterDisposable(http);
            client = new AnthropicClient { ApiKey = apiKey, BaseUrl = baseUrl, Timeout = options.Timeout, HttpClient = http };
        }

        context.RegisterDisposable(client);

        return model => client.AsIChatClient(model);
    }
}
