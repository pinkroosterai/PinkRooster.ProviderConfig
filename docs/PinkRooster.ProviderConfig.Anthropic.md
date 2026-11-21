![PinkRooster logo](https://raw.githubusercontent.com/pinkroosterai/PinkRooster/5b6d303a7ab57cf6374b210eaf0e36ca9c2ef403/assets/logo-120.png)

# PinkRooster.ProviderConfig.Anthropic

The anthropic provider type for PinkRooster.ProviderConfig: the Messages API of Anthropic or a compatible service.

Status: preview (`0.1.0-preview.5`). Targets .NET 10.

```text
dotnet add package PinkRooster.ProviderConfig.Anthropic --prerelease
```

## Quickstart

```csharp
using PinkRooster.ProviderConfig;
using PinkRooster.ProviderConfig.Anthropic;

services.AddProviderConfig()
    .AddAnthropic()
    .AddChatClient("claude", "claude-sonnet-4-5");
```

Configure `Providers:claude:Type` as `anthropic` and supply `Providers:claude:ApiKey` through .NET configuration (for example, via `Providers__claude__ApiKey`). See the [configuration reference](https://github.com/pinkroosterai/PinkRooster.ProviderConfig#configuration).

## Compatibility

.NET 10. Depends on `PinkRooster.ProviderConfig` and `Anthropic`.
