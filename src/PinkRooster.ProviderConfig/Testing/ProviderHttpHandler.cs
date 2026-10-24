namespace PinkRooster.ProviderConfig;

/// <summary>
/// The transport seam of the built-in provider types: when registered, their SDK clients send through this handler instead of the
/// network. A test-only seam: tests register it; production never does.
/// </summary>
public sealed class ProviderHttpHandler(HttpMessageHandler handler)
{
    /// <summary>Creates an <see cref="HttpClient"/> over the handler that leaves the handler undisposed and applies no client timeout.</summary>
    /// <returns>The client.</returns>
    public HttpClient CreateClient() => new(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
}
