![PinkRooster logo](https://raw.githubusercontent.com/pinkroosterai/PinkRooster/5b6d303a7ab57cf6374b210eaf0e36ca9c2ef403/assets/logo-120.png)

# PinkRooster.ProviderConfig.OpenAI

The openai and openai_responses provider types for PinkRooster.ProviderConfig: the Chat Completions and Responses APIs of OpenAI or a compatible service.

Status: preview (`0.1.0-preview.2`), developed against the .NET 10 SDK.

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
