# PinkRooster.ProviderConfig

Declare AI providers in configuration, then ask for a ready client by provider ID and model.

Chat providers give you an `IChatClient` (`Microsoft.Extensions.AI`). Jev System One providers give you a Jev.Net `ITypeSafeClient` with the model already set. Each app pulls in only the SDKs it uses.

The packages are in preview and not on NuGet yet. Until then, reference the projects under `src/` by path. The repository is private: https://github.com/pinkroosterai/PinkRooster.ProviderConfig

## Packages

| Package | Adds | Pulls in |
|---|---|---|
| `PinkRooster.ProviderConfig` | `AddProviderConfig()`, `IChatClientFactory`, `ProviderOptions`, the type registry, startup validation, `ProviderHttpHandler` | `Microsoft.Extensions.AI.Abstractions`, Options, Options.ConfigurationExtensions, DependencyInjection.Abstractions, Logging.Abstractions |
| `PinkRooster.ProviderConfig.OpenAI` | types `openai` and `openai_responses`, `AddOpenAI()` | the core, `Microsoft.Extensions.AI.OpenAI` |
| `PinkRooster.ProviderConfig.Anthropic` | type `anthropic`, `AddAnthropic()` | the core, `Anthropic` |
| `PinkRooster.ProviderConfig.Jev` | type `jev`, `AddJev()`, `AddSystemOneClient(...)`, `ISystemOneClientFactory` | the core, `Jev.Net` |

The core has no dependency on any provider SDK. Reference only the packages you need.

## Configuration

Providers live under one top-level section, `Providers`. The key is the provider ID, and IDs are matched ignoring case.

```json
{
  "Providers": {
    "openai":  { "Type": "openai_responses", "ApiKey": "sk-...", "Timeout": "00:02:00" },
    "chat":    { "Type": "openai", "ApiKey": "sk-..." },
    "ollama":  { "Type": "openai", "Endpoint": "http://localhost:11434/v1" },
    "claude":  { "Type": "anthropic", "ApiKey": "sk-ant-..." },
    "jev":     { "Type": "jev", "ApiKey": "..." }
  }
}
```

`AddProviderConfig()` binds `Providers` from the `IConfiguration` in the container, if there is one. You do not call `BindConfiguration` yourself. You can also add or override providers in code with `Configure`, which runs after the binding:

```csharp
services.AddProviderConfig().Configure(options =>
    options.Providers["local"] = new ProviderOptions { Type = "openai", Endpoint = new Uri("http://localhost:11434/v1") });
```

Providers are read once at startup. They are not reloaded while the app runs.

## Registration

```csharp
using PinkRooster.ProviderConfig;
using PinkRooster.ProviderConfig.Anthropic;
using PinkRooster.ProviderConfig.Jev;
using PinkRooster.ProviderConfig.OpenAI;

services.AddProviderConfig()                       // binds "Providers", registers the factories, validates on start
    .AddOpenAI()                                   // openai, openai_responses
    .AddAnthropic()                                // anthropic
    .AddJev()                                      // jev
    .AddChatClient("openai", "gpt-5-mini")         // unkeyed IChatClient
    .AddChatClient("openai", "gpt-5", serviceKey: "frontier")   // keyed IChatClient
    .AddSystemOneClient("jev", "jev-1.14");        // ITypeSafeClient (PinkRooster.ProviderConfig.Jev)
```

`AddProviderConfig()`, `AddOpenAI()`, `AddAnthropic()` and `AddJev()` are safe to call more than once, from the app and from other libraries, in any order. All calls share one registry and one validator. `AddChatClient` and `AddSystemOneClient` add a registration per call, so the last unkeyed one wins. The provider IDs you pass to them are checked when the service is first resolved, not at startup. Registering a different creator under a type name that is already taken throws.

To get a client without registering it, inject the factory:

```csharp
using Jev.Net;

IChatClient chat = chatClientFactory.GetChatClient("openai", "gpt-5-mini");
ITypeSafeClient jev = systemOneClientFactory.GetSystemOneClient("jev", "jev-1.14");
```

`IChatClientFactory` is in the core. `ISystemOneClientFactory` is in the Jev package. Clients are created lazily, once per provider, and disposed when the container shuts down. For chat clients, a `ChatOptions.ModelId` on a call overrides the default model. Asking for the wrong kind of client fails with a clear error. For example, a chat client from a `jev` provider.

## Provider types

| Type | Package | Client | Required settings | Other settings |
|---|---|---|---|---|
| `openai` | `.OpenAI` | `IChatClient` (Chat Completions) | `ApiKey`, unless `Endpoint` is set | `Endpoint`, `Timeout` |
| `openai_responses` | `.OpenAI` | `IChatClient` (Responses) | `ApiKey`, unless `Endpoint` is set | `Endpoint`, `Timeout`, `Store` |
| `anthropic` | `.Anthropic` | `IChatClient` | `ApiKey` | `Endpoint`, `Timeout` |
| `jev` | `.Jev` | `ITypeSafeClient` | `ApiKey` | `Endpoint`, `Timeout` |

- `openai` and `openai_responses` with an `Endpoint` and no `ApiKey` send the placeholder key `providerconfig-no-key`. That suits local servers such as Ollama.
- `openai_responses` sends `store: false` unless you set `Store: true`. `Store` on any other type is a validation error.
- `openai_responses` also turns a failure that the upstream reports inside a successful response into an exception.
- `Timeout` must be greater than zero. For `openai` and `openai_responses` it bounds the wait for the response headers and each gap between streamed chunks, not the whole stream. For `anthropic` it is the SDK request timeout; a whole (not streamed) request defaults to 30 seconds, so set it for long answers.
- `jev` pins the model: a call that passes no `SystemOneOptions`, or leaves `Model` null, gets the model ID from `AddSystemOneClient` or `GetSystemOneClient`. A model the caller sets wins, and the caller's other options are kept. `Endpoint` defaults to `https://api.typesafe.ai`.

## Validation

Providers are validated at startup (`ValidateOnStart`). Messages start with `Providers:{id}`. The rules:

- The provider ID uses valid characters.
- `Type` is set and known. The error lists the known types.
- `Endpoint` is an absolute http or https URL.
- `Timeout` is greater than zero.
- `Store` is set only on `openai_responses`.
- Each type's own rules from the table above (required `ApiKey`).

The API key never appears in a message, a log or a validation error.

## Ambient variables never apply

Configuration is the only source of an endpoint, a key or a model. These variables are ignored: `OPENAI_*`, `ANTHROPIC_*`, `TYPESAFE_API_KEY`, `TYPESAFE_BASE_URL` and `TYPESAFE_DEFAULT_MODEL`. Every client gets an explicit endpoint and key, and every Jev call carries a model. A blank `ApiKey` on `jev` or `anthropic` is an error instead of a fallback to the environment.

Use the standard configuration providers if you want environment variables, for example `Providers__openai__ApiKey`.

## Plain-HTTP warning

A provider with an `ApiKey` and an `http://` `Endpoint` sends its key unencrypted, because the SDKs do not upgrade to HTTPS. When that happens, and the host is not loopback, this warning is logged once, on the first validation, for the providers configured then:

```text
Upstream provider '{ProviderId}' sends its API key over plain HTTP to {Host}. Use https unless the network is trusted.
```

The first validation happens at startup in a hosted app, or on first use without a host. It uses the container's `ILoggerFactory` and is silent if there is none. Repeated options reads do not log it again, and providers added later are not warned about. It applies to every type.

## Custom provider types

Add a type with `AddProviderType<TClient>`. The creator runs once per configured provider and returns a function from model ID to client:

```csharp
services.AddProviderConfig()
    .AddProviderType<IChatClient>("custom", context =>
    {
        ProviderOptions options = context.Options;
        return model => new MyChatClient(options, model);
    });
```

`context.RegisterDisposable(...)` hands an object to the factory, which disposes it once with the container. Pass a `ProviderTypeOptions` to add validation rules for the type. The creator runs once per provider, the first time a client is asked for; if it throws, that provider keeps failing until the app restarts. To get a client of a custom kind, call `IProviderClientFactory.GetClient<TClient>(providerId, modelId)`. Use a static method group for the creator, not a lambda, if the registration may run twice: a repeat registration is a no-op only when the creators are equal.

## Testing

`ProviderHttpHandler` is a test-only seam. When it is registered, the built-in types send through your handler instead of the network:

```csharp
services.AddSingleton(new ProviderHttpHandler(handler));
```

Production code never registers it.

The live tests call real APIs and skip unless their variables are set:

| Tests | Variables |
|---|---|
| OpenAI (`openai`, `openai_responses`) | `PROVIDERCONFIG_LIVE_OPENAI_KEY`, `PROVIDERCONFIG_LIVE_OPENAI_MODEL` |
| Anthropic | `PROVIDERCONFIG_LIVE_ANTHROPIC_KEY`, `PROVIDERCONFIG_LIVE_ANTHROPIC_MODEL` |
| Jev | `TYPESAFE_LIVE_TESTS=1` and `TYPESAFE_API_KEY` |

Build and test:

```text
dotnet build PinkRooster.ProviderConfig.slnx -c Release
dotnet test --solution PinkRooster.ProviderConfig.slnx -c Release
```

## License

MIT. See [LICENSE](LICENSE).
