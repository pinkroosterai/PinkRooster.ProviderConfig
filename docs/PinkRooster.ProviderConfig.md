![PinkRooster logo](https://raw.githubusercontent.com/pinkroosterai/PinkRooster/5b6d303a7ab57cf6374b210eaf0e36ca9c2ef403/assets/logo-120.png)

# PinkRooster.ProviderConfig

Configuration-driven setup for `Microsoft.Extensions.AI` chat clients and text embedding generators. The core provides a type registry, client factories, and startup validation without depending on any provider SDK.

Status: preview (`0.1.0-preview.5`). Targets .NET 10.

```text
dotnet add package PinkRooster.ProviderConfig --prerelease
```

## Quickstart

Install and register an adapter alongside the core to use a built-in provider type. Start with the [runnable OpenAI quickstart](https://github.com/pinkroosterai/PinkRooster.ProviderConfig#quickstart), or see the [adapter list](https://github.com/pinkroosterai/PinkRooster.ProviderConfig#packages). To register your own provider instead, use [`AddProviderType<TClient>`](https://github.com/pinkroosterai/PinkRooster.ProviderConfig#custom-provider-types).

## Model Defaults Configuration

Default generation parameters can be declared per model under `Providers:<id>:Models:<modelId>`:

```json
{
  "Providers": {
    "openai": {
      "Type": "openai",
      "Models": {
        "gpt-4.1-mini": {
          "Temperature": 0.2,
          "MaxOutputTokens": 4096
        }
      }
    }
  }
}
```

Supply `Providers:openai:ApiKey` separately through .NET configuration, for example using the `Providers__openai__ApiKey` environment variable.

Supported settings are `Temperature` (0–2), `ReasoningEffort` (a defined `Microsoft.Extensions.AI.ReasoningEffort` value), `MaxOutputTokens` (positive), `TopP` (0–1), `TopK` (positive), `FrequencyPenalty` and `PresencePenalty` (−2–2), `Seed`, `StopSequences`, and `AdditionalProperties`. Invalid settings fail startup validation with the provider, model, and property path.

Model IDs are case-insensitive. Every call selects defaults using `ChatOptions.ModelId` when supplied, otherwise the model ID used to create the client. Unlisted models receive no defaults. Both normal and streaming chat calls use this behavior.

When an `IChatClient` is resolved via `IChatClientFactory` or `AddChatClient`, it automatically uses these configured defaults. Any option supplied by the caller at invocation time overrides the configured default, including zero temperature. Caller options are cloned before defaults are merged. `AdditionalProperties` are merged by key, with caller keys taking precedence.

An explicit empty `StopSequences` list disables configured stop sequences. Only a null list receives defaults. Floating-point generation settings must be finite and within their documented ranges.

Configuration stores scalar values as strings. For model `AdditionalProperties`, boolean values are converted to `bool`, integers to `long`, and other finite numbers to `double`, using invariant culture. Other values remain strings, and null values remain null. This conversion also applies to quoted numeric or boolean strings. To pass a literal string such as `"2048"` or `"true"`, set that property through the builder's `Configure` callback, which runs after configuration binding.

### Embedding dimensions

For OpenAI embedding providers, set `Providers:<id>:Models:<modelId>:Dimensions` to request a specific vector length. The value must be a positive integer; the upstream model must support it. This setting is separate from the chat defaults above. See the [embeddings walkthrough](https://github.com/pinkroosterai/PinkRooster.ProviderConfig#generate-embeddings).

## Compatibility

.NET 10. Depends on `Microsoft.Extensions.AI.Abstractions`.
