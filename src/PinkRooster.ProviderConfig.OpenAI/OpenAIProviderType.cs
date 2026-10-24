// OPENAI001: GetResponsesClient, ResponsesClient and its AsIChatClient overload of the OpenAI SDK are experimental.
#pragma warning disable OPENAI001

using System.ClientModel;
using System.ClientModel.Primitives;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using OpenAI.Responses;

namespace PinkRooster.ProviderConfig.OpenAI;

/// <summary>
/// The built-in <c>openai</c> and <c>openai_responses</c> provider types: the Chat Completions API and the Responses API of OpenAI or a
/// compatible service.
/// </summary>
internal static class OpenAIProviderType
{
    private const string DefaultEndpoint = "https://api.openai.com/v1/";
    private const string PlaceholderKey = "providerconfig-no-key";

    /// <summary>Creates the SDK client for a provider. The endpoint is always set, so no <c>OPENAI_*</c> environment variable can redirect it.</summary>
    public static OpenAIClient CreateClient(ProviderContext context)
    {
        ProviderOptions options = context.Options;
        UriBuilder endpoint = new(options.Endpoint ?? new Uri(DefaultEndpoint));
        if (!endpoint.Path.EndsWith('/'))
        {
            endpoint.Path += "/";
        }

        OpenAIClientOptions clientOptions = new() { Endpoint = endpoint.Uri };
        if (options.Timeout is { } timeout)
        {
            clientOptions.NetworkTimeout = timeout;
        }

        ProviderHttpHandler? seam = context.Services.GetService<ProviderHttpHandler>();
        if (seam is not null)
        {
            // The seam client wraps a shared handler with disposeHandler: false, so disposing it leaves the handler alone.
            HttpClient http = seam.CreateClient();
            context.RegisterDisposable(http);
            clientOptions.Transport = new HttpClientPipelineTransport(http);
        }

        string key = string.IsNullOrWhiteSpace(options.ApiKey) ? PlaceholderKey : options.ApiKey;
        return new OpenAIClient(new ApiKeyCredential(key), clientOptions);
    }

    /// <summary>Creates the client function of an <c>openai</c> provider: one SDK client, a chat client per model.</summary>
    public static Func<string, IChatClient> CreateChatCompletions(ProviderContext context)
    {
        OpenAIClient client = CreateClient(context);
        return model => client.GetChatClient(model).AsIChatClient();
    }

    /// <summary>
    /// Creates the client function of an <c>openai_responses</c> provider: one Responses client, a wrapped chat client per model that sends
    /// the provider's <c>store</c> setting, false unless configured.
    /// </summary>
    public static Func<string, IChatClient> CreateResponses(ProviderContext context)
    {
        ResponsesClient responses = CreateClient(context).GetResponsesClient();
        bool store = context.Options.Store ?? false;
        return model => new ResponsesChatClient(responses.AsIChatClient(model), store);
    }
}
