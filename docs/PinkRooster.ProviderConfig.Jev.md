![PinkRooster logo](https://raw.githubusercontent.com/pinkroosterai/PinkRooster/5b6d303a7ab57cf6374b210eaf0e36ca9c2ef403/assets/logo-120.png)

# PinkRooster.ProviderConfig.Jev

The jev provider type for PinkRooster.ProviderConfig: the Jev System One API of TypeSafe, through Jev.Net.

Status: preview (`0.1.0-preview.2`), developed against the .NET 10 SDK.

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
