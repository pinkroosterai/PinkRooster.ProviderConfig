<img src="https://raw.githubusercontent.com/pinkroosterai/PinkRooster/a3815ce62a2491a8ee1c7401e1e48aa43ef20a85/assets/logo.png" alt="PinkRooster logo" width="120" />

# PinkRooster.ProviderConfig.OpenAI

The openai and openai_responses provider types for PinkRooster.ProviderConfig: the Chat Completions and Responses APIs of OpenAI or a compatible service.

Status: preview (`0.1.0-preview.1`), developed against the .NET 10 SDK.

```text
dotnet add package PinkRooster.ProviderConfig.OpenAI --prerelease
```

## Quickstart

```csharp
using PinkRooster.ProviderConfig;
using PinkRooster.ProviderConfig.OpenAI;

services.AddProviderConfig()
    .AddOpenAI()
    .AddChatClient("openai", "gpt-5-mini");
```

## Compatibility

.NET 10. Depends on `PinkRooster.ProviderConfig` and `Microsoft.Extensions.AI.OpenAI`.
