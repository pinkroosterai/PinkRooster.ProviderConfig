namespace PinkRooster.ProviderConfig;

/// <summary>The provider settings of an application, keyed by provider id.</summary>
public sealed class ProviderConfigOptions
{
    /// <summary>The configuration section the providers are bound from.</summary>
    public const string SectionName = "Providers";

    /// <summary>The providers by id. Ids are compared ignoring case.</summary>
    public IDictionary<string, ProviderOptions> Providers { get; } =
        new Dictionary<string, ProviderOptions>(StringComparer.OrdinalIgnoreCase);
}
