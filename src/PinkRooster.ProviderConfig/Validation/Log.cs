using Microsoft.Extensions.Logging;

namespace PinkRooster.ProviderConfig;

/// <summary>Source-generated log messages of the provider configuration.</summary>
internal static partial class Log
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "Upstream provider '{ProviderId}' sends its API key over plain HTTP to {Host}. Use https unless the network is trusted.")]
    public static partial void ProviderKeyOverHttp(ILogger logger, string providerId, string host);
}
