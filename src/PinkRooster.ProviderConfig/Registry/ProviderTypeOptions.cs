namespace PinkRooster.ProviderConfig;

/// <summary>Optional behaviour of a registered provider type.</summary>
public sealed class ProviderTypeOptions
{
    /// <summary>
    /// Checks the settings of a provider of this type. Returns one message per problem, without the <c>Providers:{id}:</c> prefix;
    /// the validator adds it. Unset means the type has no checks of its own.
    /// </summary>
    public Func<ProviderOptions, IEnumerable<string>>? Validate { get; init; }

    /// <summary>Whether the type honours <see cref="ProviderOptions.Store"/>. The validator rejects <c>Store</c> on other types.</summary>
    public bool UsesStore { get; init; }
}
