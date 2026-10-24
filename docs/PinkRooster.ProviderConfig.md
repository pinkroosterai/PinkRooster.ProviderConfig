<img src="https://raw.githubusercontent.com/pinkroosterai/PinkRooster/a3815ce62a2491a8ee1c7401e1e48aa43ef20a85/assets/logo.png" alt="PinkRooster logo" width="120" />

# PinkRooster.ProviderConfig

Configuration-driven provider setup for Microsoft.Extensions.AI chat clients: a provider type registry, a chat client factory and configuration validation.

Status: preview (`0.1.0-preview.1`), developed against the .NET 10 SDK.

```text
dotnet add package PinkRooster.ProviderConfig --prerelease
```

## Quickstart

```csharp
using PinkRooster.ProviderConfig;

services.AddProviderConfig()
    .AddChatClient("openai", "gpt-5-mini");
```

## Compatibility

.NET 10. Depends on `Microsoft.Extensions.AI.Abstractions`.
