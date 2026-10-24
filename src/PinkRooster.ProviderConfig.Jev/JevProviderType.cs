using Jev.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PinkRooster.ProviderConfig.Jev;

/// <summary>The built-in <c>jev</c> provider type: the Jev System One API of TypeSafe, through Jev.Net.</summary>
internal static class JevProviderType
{
    private const string DefaultEndpoint = "https://api.typesafe.ai";

    /// <summary>
    /// Creates the client function of a <c>jev</c> provider: one SDK client, a model-pinned wrapper per model. The key and base URL are
    /// always set, so no <c>TYPESAFE_*</c> environment variable can redirect the client.
    /// </summary>
    /// <param name="context">The provider's ID, settings and the application services.</param>
    /// <returns>The function that creates a System One client for a model ID.</returns>
    public static Func<string, ITypeSafeClient> Create(ProviderContext context)
    {
        ProviderOptions options = context.Options;
        string apiKey = options.ApiKey ?? string.Empty;
        string baseUrl = options.Endpoint?.ToString().TrimEnd('/') ?? DefaultEndpoint;
        ILoggerFactory? loggerFactory = context.Services.GetService<ILoggerFactory>();

        // HttpClient is init-only, so it is set only through its own initializer when the test seam is present.
        ProviderHttpHandler? seam = context.Services.GetService<ProviderHttpHandler>();
        TypeSafeClient client;
        if (seam is null)
        {
            client = new TypeSafeClient(new TypeSafeClientOptions { ApiKey = apiKey, BaseUrl = baseUrl, Timeout = options.Timeout, LoggerFactory = loggerFactory });
        }
        else
        {
            // The seam client wraps a shared handler with disposeHandler: false, so disposing it leaves the handler alone.
            HttpClient http = seam.CreateClient();
            context.RegisterDisposable(http);
            client = new TypeSafeClient(new TypeSafeClientOptions
            {
                ApiKey = apiKey,
                BaseUrl = baseUrl,
                Timeout = options.Timeout,
                LoggerFactory = loggerFactory,
                HttpClient = http,
                DisposeHttpClient = false,
            });
        }

        context.RegisterDisposable(client);

        return model => new ModelPinnedTypeSafeClient(client, model);
    }
}
