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
    }
}
