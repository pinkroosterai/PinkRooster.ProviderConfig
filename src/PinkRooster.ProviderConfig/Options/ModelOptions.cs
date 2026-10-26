using Microsoft.Extensions.AI;

namespace PinkRooster.ProviderConfig;

/// <summary>Default execution settings for one model under a provider.</summary>
public sealed class ModelOptions
{
    /// <summary>The sampling temperature for responses (0.0 to 2.0).</summary>
    public float? Temperature { get; set; }

    /// <summary>The level of reasoning effort to apply.</summary>
    public ReasoningEffort? ReasoningEffort { get; set; }

    /// <summary>The maximum number of tokens in the generated response.</summary>
    public int? MaxOutputTokens { get; set; }

    /// <summary>The nucleus sampling factor (0.0 to 1.0).</summary>
    public float? TopP { get; set; }

    /// <summary>The number of most probable tokens to consider.</summary>
    public int? TopK { get; set; }

    /// <summary>The penalty for repeated tokens (-2.0 to 2.0).</summary>
    public float? FrequencyPenalty { get; set; }

    /// <summary>The penalty for token presence (-2.0 to 2.0).</summary>
    public float? PresencePenalty { get; set; }

    /// <summary>A seed value used by the service for reproducible output.</summary>
    public long? Seed { get; set; }

    /// <summary>Stop sequences that terminate generation.</summary>
    public IList<string>? StopSequences { get; set; }

    /// <summary>Custom or provider-specific parameters passed to the upstream provider.</summary>
    public IDictionary<string, object?> AdditionalProperties { get; set; } = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
