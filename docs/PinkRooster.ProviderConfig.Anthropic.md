<img src="https://raw.githubusercontent.com/pinkroosterai/PinkRooster/a3815ce62a2491a8ee1c7401e1e48aa43ef20a85/assets/logo.png" alt="PinkRooster logo" width="120" />

# PinkRooster.ProviderConfig.Anthropic

The anthropic provider type for PinkRooster.ProviderConfig: the Messages API of Anthropic or a compatible service.

Status: preview (`0.1.0-preview.1`), developed against the .NET 10 SDK.

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

## Compatibility

.NET 10. Depends on `PinkRooster.ProviderConfig` and `Anthropic`.
