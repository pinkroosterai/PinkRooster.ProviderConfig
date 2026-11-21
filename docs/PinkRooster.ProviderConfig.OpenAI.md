![PinkRooster logo](https://raw.githubusercontent.com/pinkroosterai/PinkRooster/5b6d303a7ab57cf6374b210eaf0e36ca9c2ef403/assets/logo-120.png)

# PinkRooster.ProviderConfig.OpenAI

The `openai`, `openai_responses`, `openai_embedding`, and `openai_embeddings` provider types for PinkRooster.ProviderConfig: the Chat Completions, Responses, and Embeddings APIs of OpenAI or a compatible service.

Status: preview (`0.1.0-preview.6`). Targets .NET 10.

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

## SDK retries for embeddings

For `openai_embedding` and `openai_embeddings`, set `Providers:<id>:MaxRetries` (or `ProviderOptions.MaxRetries` in code). For the `search` provider above, set `Providers__search__MaxRetries=0` if your application owns the retry loop.

- Omit the setting or use null to keep the SDK's default policy.
- Use `0` for a single attempt with no SDK retries.
- Use a positive integer to cap the number of *additional* attempts. The SDK still decides which failures to retry and how long to wait.

The cap covers all embedding models on that provider. Chat Completions and Responses keep their existing retry policies and reject this setting.

## Compatibility

.NET 10. Depends on `PinkRooster.ProviderConfig` and `Microsoft.Extensions.AI.OpenAI`.
