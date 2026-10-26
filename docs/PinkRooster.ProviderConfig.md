![PinkRooster logo](https://raw.githubusercontent.com/pinkroosterai/PinkRooster/5b6d303a7ab57cf6374b210eaf0e36ca9c2ef403/assets/logo-120.png)

# PinkRooster.ProviderConfig

Configuration-driven provider setup for Microsoft.Extensions.AI chat clients: a provider type registry, a chat client factory and configuration validation.

Status: preview (`0.1.0-preview.3`), developed against the .NET 10 SDK.

```text
dotnet add package PinkRooster.ProviderConfig --prerelease
```

## Quickstart

```csharp
using PinkRooster.ProviderConfig;

services.AddProviderConfig()
    .AddChatClient("openai", "gpt-5-mini");
```

## Model Defaults Configuration

Default generation parameters can be declared per model under `Providers:<id>:Models:<modelId>`:

```json
{
  "Providers": {
    "openai": {
      "Type": "openai",
      "ApiKey": "sk-...",
      "Models": {
        "gpt-5": {
          "Temperature": 0.2,
          "ReasoningEffort": "High",
          "MaxOutputTokens": 4096,
          "AdditionalProperties": {
            "custom_budget_tokens": 2048
          }
        },
        "gpt-5-mini": {
          "Temperature": 0.7,
          "ReasoningEffort": "Low"
        }
      }
    }
  }
}
```

Supported settings are `Temperature` (0–2), `ReasoningEffort` (a defined `Microsoft.Extensions.AI.ReasoningEffort` value), `MaxOutputTokens` (positive), `TopP` (0–1), `TopK` (positive), `FrequencyPenalty` and `PresencePenalty` (−2–2), `Seed`, `StopSequences`, and `AdditionalProperties`. Invalid settings fail startup validation with the provider, model, and property path.

Model IDs are case-insensitive. Every call selects defaults using `ChatOptions.ModelId` when supplied, otherwise the model ID used to create the client. Unlisted models receive no defaults. Both normal and streaming calls use this behavior; non-chat clients are unaffected.

When an `IChatClient` is resolved via `IChatClientFactory` or `AddChatClient`, it automatically uses these configured defaults. Any option supplied by the caller at invocation time overrides the configured default, including zero temperature. Caller options are cloned before defaults are merged. `AdditionalProperties` are merged by key, with caller keys taking precedence.

An explicit empty `StopSequences` list disables configured stop sequences. Only a null list receives defaults. Floating-point generation settings must be finite and within their documented ranges.

Configuration stores scalar values as strings. For model `AdditionalProperties`, boolean values are converted to `bool`, integers to `long`, and other finite numbers to `double`, using invariant culture. Other values remain strings, and null values remain null. This conversion also applies to quoted numeric or boolean strings. To pass a literal string such as `"2048"` or `"true"`, set that property through the builder's `Configure` callback, which runs after configuration binding.

## Compatibility

.NET 10. Depends on `Microsoft.Extensions.AI.Abstractions`.
