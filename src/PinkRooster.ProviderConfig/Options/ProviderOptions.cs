namespace PinkRooster.ProviderConfig;

/// <summary>Settings for one upstream provider, configured under <c>Providers:&lt;id&gt;</c>.</summary>
public sealed class ProviderOptions
{
    /// <summary>The provider type, for example <c>openai_responses</c>.</summary>
    public string? Type { get; set; }

    /// <summary>The base endpoint of the upstream service.</summary>
    public Uri? Endpoint { get; set; }

    /// <summary>The API key sent to the upstream service.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The request timeout for calls to the upstream service. For <c>openai</c> and <c>openai_responses</c> it bounds the wait for the response
    /// headers and each gap between streamed chunks, not the whole stream; unset uses the SDK pipeline default. For <c>anthropic</c> it is the
    /// SDK's request timeout: by default a whole (not streamed) request is sent with a 30 second timeout, so set it for long answers.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>Whether the upstream stores responses. Applies to <c>openai_responses</c> only; defaults to false.</summary>
    public bool? Store { get; set; }

    /// <summary>Model-specific execution defaults configured under <c>Providers:&lt;id&gt;:Models:&lt;modelId&gt;</c>.</summary>
    public IDictionary<string, ModelOptions> Models { get; } = new Dictionary<string, ModelOptions>(StringComparer.OrdinalIgnoreCase);
}
