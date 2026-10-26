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
        configuration?.GetSection(ProviderConfigOptions.SectionName).Bind(options.Providers);

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
