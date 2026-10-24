using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PinkRooster.ProviderConfig;

/// <summary>Checks the configured upstream providers, so a mistake fails at startup and not at the first request.</summary>
internal sealed class ProvidersValidator(ProviderTypeRegistry providerTypes, IServiceProvider services) : IValidateOptions<ProviderConfigOptions>
{
    private int _warned;

    public ValidateOptionsResult Validate(string? name, ProviderConfigOptions options)
    {
        List<string> failures = [];
        foreach ((string id, ProviderOptions provider) in options.Providers)
        {
            ValidateProvider(id, provider, failures);
        }

        // The options are validated on every read (IOptions, IOptionsMonitor, IOptionsSnapshot), so warn on the first one only.
        if (Interlocked.Exchange(ref _warned, 1) == 0)
        {
            WarnAboutKeysOverHttp(options);
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private void ValidateProvider(string id, ProviderOptions provider, List<string> failures)
    {
        string prefix = $"{ProviderConfigOptions.SectionName}:{id}";
        if (provider is null)
        {
            failures.Add($"{prefix} is empty.");
            return;
        }

        if (id.Length == 0 || !id.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-'))
        {
            failures.Add($"Provider ID '{id}' may only contain letters, digits, '_' and '-'.");
        }

        string known = string.Join(", ", providerTypes.Types);
        string? type = provider.Type;
        ProviderTypeRegistration? registration = null;
        if (string.IsNullOrWhiteSpace(type))
        {
            failures.Add($"{prefix}:Type is required. Known types: {known}.");
        }
        else if (!providerTypes.TryGet(type, out registration))
        {
            failures.Add($"{prefix}:Type '{type}' is not a known provider type. Known types: {known}.");
        }

        if (provider.Endpoint is { } endpoint && (!endpoint.IsAbsoluteUri || (endpoint.Scheme != Uri.UriSchemeHttp && endpoint.Scheme != Uri.UriSchemeHttps)))
        {
            failures.Add($"{prefix}:Endpoint must be an absolute http or https URL.");
        }

        if (registration?.Options.Validate is { } validate)
        {
            foreach (string message in validate(provider))
            {
                failures.Add($"{prefix}:{message}");
            }
        }

        if (provider.Timeout is { } timeout && timeout <= TimeSpan.Zero)
        {
            failures.Add($"{prefix}:Timeout must be greater than zero.");
        }

        if (provider.Store is not null && registration?.Options.UsesStore != true)
        {
            string[] storeTypes = [.. providerTypes.Types.Where(storeType => providerTypes.TryGet(storeType, out ProviderTypeRegistration? other) && other.Options.UsesStore)];
            if (storeTypes.Length == 0)
            {
                failures.Add($"{prefix}:Store is not used by type '{type}'.");
            }
            else
            {
                string noun = storeTypes.Length == 1 ? "type" : "types";
                failures.Add($"{prefix}:Store applies only to {noun} {string.Join(", ", storeTypes.Select(storeType => $"'{storeType}'"))}.");
            }
        }
    }

    private void WarnAboutKeysOverHttp(ProviderConfigOptions options)
    {
        ILogger? logger = services.GetService<ILoggerFactory>()?.CreateLogger("PinkRooster.ProviderConfig");
        if (logger is null)
        {
            return;
        }

        foreach ((string id, ProviderOptions? provider) in options.Providers)
        {
            if (provider is not null
                && !string.IsNullOrWhiteSpace(provider.ApiKey)
                && provider.Endpoint is { IsAbsoluteUri: true, Scheme: "http" } endpoint
                && !endpoint.IsLoopback)
            {
                Log.ProviderKeyOverHttp(logger, id, endpoint.Host);
            }
        }
    }
}
