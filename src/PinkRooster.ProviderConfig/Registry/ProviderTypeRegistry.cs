using System.Diagnostics.CodeAnalysis;

namespace PinkRooster.ProviderConfig;

/// <summary>A registered provider type: the client kind it creates and the function that creates it.</summary>
internal sealed record ProviderTypeRegistration(string Name, Type ClientType, Delegate Create, ProviderTypeOptions Options);

/// <summary>The provider types that were registered, by name ignoring case.</summary>
internal sealed class ProviderTypeRegistry
{
    private readonly System.Threading.Lock _gate = new();
    private readonly Dictionary<string, ProviderTypeRegistration> _types = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Types
    {
        get
        {
            lock (_gate)
            {
                return [.. _types.Keys.Order(StringComparer.Ordinal)];
            }
        }
    }

    /// <summary>
    /// Adds a registration. Registering the same name again with the same client kind and an equal creator is a no-op, so an app can
    /// call a type's <c>Add…</c> method twice; any other registration under a taken name throws.
    /// </summary>
    public void Add(ProviderTypeRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentException.ThrowIfNullOrWhiteSpace(registration.Name, nameof(registration.Name));
        lock (_gate)
        {
            if (_types.TryGetValue(registration.Name, out ProviderTypeRegistration? existing))
            {
                if (existing.ClientType == registration.ClientType && existing.Create.Equals(registration.Create))
                {
                    return;
                }

                throw new ArgumentException($"A provider type '{registration.Name}' is already registered.", "type");
            }

            _types.Add(registration.Name, registration);
        }
    }

    public bool TryGet(string type, [MaybeNullWhen(false)] out ProviderTypeRegistration registration)
    {
        lock (_gate)
        {
            return _types.TryGetValue(type, out registration);
        }
    }
}
