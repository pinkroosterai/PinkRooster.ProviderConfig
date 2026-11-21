![PinkRooster logo](https://raw.githubusercontent.com/pinkroosterai/PinkRooster/5b6d303a7ab57cf6374b210eaf0e36ca9c2ef403/assets/logo-120.png)

# PinkRooster.ProviderConfig.OpenAI

The `openai`, `openai_responses`, `openai_embedding`, and `openai_embeddings` provider types for PinkRooster.ProviderConfig: the Chat Completions, Responses, and Embeddings APIs of OpenAI or a compatible service.

Status: preview (`0.1.0-preview.5`). Targets .NET 10.

```text
dotnet add package PinkRooster.ProviderConfig.OpenAI --prerelease
```

## Quickstart

```csharp
using PinkRooster.ProviderConfig;
using PinkRooster.ProviderConfig.OpenAI;

services.AddProviderConfig()
    .AddOpenAI()
    .AddChatClient("chat", "gpt-5-mini")
    .AddEmbeddingGenerator("search", "text-embedding-3-small");
```

Configure `Providers:chat:Type` as `openai_responses` and `Providers:search:Type` as `openai_embedding`. Supply each provider’s key through configuration (for example, `Providers__chat__ApiKey` and `Providers__search__ApiKey`). Each registration is optional; use only the client you need. See the [runnable chat example](https://github.com/pinkroosterai/PinkRooster.ProviderConfig#quickstart) and [embeddings walkthrough](https://github.com/pinkroosterai/PinkRooster.ProviderConfig#generate-embeddings).

## Compatibility

.NET 10. Depends on `PinkRooster.ProviderConfig` and `Microsoft.Extensions.AI.OpenAI`.
