<img src="https://raw.githubusercontent.com/pinkroosterai/PinkRooster/a3815ce62a2491a8ee1c7401e1e48aa43ef20a85/assets/logo.png" alt="PinkRooster logo" width="120" />

# PinkRooster.ProviderConfig.Jev

The jev provider type for PinkRooster.ProviderConfig: the Jev System One API of TypeSafe, through Jev.Net.

Status: preview (`0.1.0-preview.1`), developed against the .NET 10 SDK.

```text
dotnet add package PinkRooster.ProviderConfig.Jev --prerelease
```

## Quickstart

```csharp
using PinkRooster.ProviderConfig;
using PinkRooster.ProviderConfig.Jev;

services.AddProviderConfig()
    .AddJev()
    .AddSystemOneClient("jev", "jev-1.14");
```

## Compatibility

.NET 10. Depends on `PinkRooster.ProviderConfig` and `Jev.Net`.
