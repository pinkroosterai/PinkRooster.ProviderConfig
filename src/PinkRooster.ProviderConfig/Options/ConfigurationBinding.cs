using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace PinkRooster.ProviderConfig;

/// <summary>Binds the <c>Providers</c> configuration section into <see cref="ProviderConfigOptions"/>, when an <see cref="IConfiguration"/> is registered.</summary>
internal sealed class ConfigurationBinding(IServiceProvider services) : IConfigureOptions<ProviderConfigOptions>
{
    public void Configure(ProviderConfigOptions options)
    {
        IConfiguration? configuration = services.GetService<IConfiguration>();
        if (configuration?.GetSection(ProviderConfigOptions.SectionName) is { } providers)
        {
            ValidateRetryConfiguration(providers);
            providers.Bind(options.Providers);
        }

        foreach (ProviderOptions provider in options.Providers.Values)
        {
            foreach (ModelOptions? model in provider.Models.Values)
            {
                if (model?.AdditionalProperties is not { } properties)
                {
                    continue;
                }

                // IConfiguration exposes scalar values as strings, regardless of their JSON token type.
                foreach ((string key, object? value) in properties.ToArray())
                {
                    if (value is string text)
                    {
                        properties[key] = ParseScalar(text);
                    }
                }
            }
        }
    }

    private static void ValidateRetryConfiguration(IConfigurationSection providers)
    {
        List<string> failures = [];
        foreach (IConfigurationSection provider in providers.GetChildren())
        {
            // Dictionary binding can drop malformed entries and treat an empty string as null.
            IConfigurationSection retries = provider.GetSection(nameof(ProviderOptions.MaxRetries));
            if (retries.GetChildren().Any()
                || (retries.Value is { } value && !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
            {
                failures.Add($"{retries.Path} must be an integer or null.");
            }
        }

        if (failures.Count > 0)
        {
            throw new OptionsValidationException(Microsoft.Extensions.Options.Options.DefaultName, typeof(ProviderConfigOptions), failures);
        }
    }

    private static object ParseScalar(string text)
    {
        if (bool.TryParse(text, out bool boolean))
        {
            return boolean;
        }

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer))
        {
            return integer;
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && double.IsFinite(number))
        {
            return number;
        }

        return text;
    }
}
