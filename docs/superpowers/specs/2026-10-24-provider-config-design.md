# PinkRooster.ProviderConfig: design

Date: 2026-10-24. Status: draft, waiting for approval.

## Intent

PeckOrder has a small, well-tested provider layer (`src/PinkRooster.PeckOrder.AspNetCore/Providers`, 476 lines). Apps declare
upstream AI providers by ID in configuration (`Type`, `Endpoint`, `ApiKey`, `Timeout`, `Store`) and ask
`IChatClientFactory.GetChatClient(providerId, modelId)` for a ready `IChatClient`. The owner wants to reuse this layer in several
projects, starting with Gizzard. They also want **Jev System One** models (TypeSafe AI, through the `Jev.Net` package) to be
configurable the same way.

So the work is to extract the layer into a stand-alone library, `PinkRooster.ProviderConfig`, add a Jev provider type, and switch
PeckOrder and Gizzard over to it.

## Goals

1. A reusable library. Apps configure providers once under a shared top-level `Providers` section and get:
   - an `IChatClient` (Microsoft.Extensions.AI) for chat providers, and
   - a Jev.Net `ITypeSafeClient` with the model already set, for Jev providers.
2. PeckOrder's current behaviour is preserved (details under "Behaviour carried over unchanged").
3. Each consumer pulls in only the SDKs it uses.
4. DI helpers that register a configured provider/model as an unkeyed or keyed service. Gizzard's core takes its chat clients
   this way.
5. PeckOrder, Gizzard.Jev and the Gizzard sample CLI are switched over in the same piece of work.

## Non-goals

- Publishing to NuGet. For now PeckOrder and Gizzard use relative-path project references (see Constraints).
- New chat provider types: native Ollama, Azure OpenAI and so on. Ollama keeps working as `openai` with an `Endpoint`.
- Reloading configuration while the app runs. Providers are still read once at startup.
- Presenting Jev as an `IChatClient`.
- Retry or resilience policies beyond what each SDK already does.

## Anti-goals (failure even if it "works")

- A consumer has to pull in an SDK it doesn't use. For example, Gizzard.Jev must not bring in the Anthropic or OpenAI SDKs.
- An ambient environment variable redirects a client or supplies its credentials or model (`OPENAI_*`, `ANTHROPIC_*`,
  `TYPESAFE_API_KEY`, `TYPESAFE_BASE_URL`, `TYPESAFE_DEFAULT_MODEL`). Configuration is the only source.
- A test calls a real API. Live tests stay opt-in, as they are in PeckOrder today.
- PeckOrder's existing gateway behaviour regresses, or its test suite loses coverage of the provider paths.

## Constraints

- `net10.0`, `Nullable` enabled, `LangVersion` latest (C# 14), `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, and
  `ImplicitUsings`. These match PeckOrder's `Directory.Build.props`.
- Central package management (`Directory.Packages.props`). Versions are aligned with the consumers: Microsoft.Extensions.AI /
  .Abstractions / .OpenAI 10.11.0, Anthropic 13.1.0, Jev.Net 0.5.0.
- Tests use xunit v3 on Microsoft.Testing.Platform (`global.json` `"test": { "runner": "Microsoft.Testing.Platform" }`), the same as
  PeckOrder.
- **Distribution:** project references by relative path (`../ProviderConfig/src/...`) from PeckOrder and Gizzard. Known
  consequence, accepted for now: PeckOrder can't produce a releasable package, and both repos' CI can't build, until
  ProviderConfig is on a feed or CI also checks out the sibling repo. Neither repo's CI is to be fixed in this work.
- PeckOrder is still changing (five commits landed while this design was written). Extract from PeckOrder's latest `main` at the
  time the work starts, not from any snapshot quoted here.
- The ProviderConfig repo is empty (`master`, no commits). Layout mirrors PeckOrder: `src/`, `tests/`, `docs/`, a `.slnx`,
  `Directory.Build.props` with MIT package metadata and SourceLink, `.editorconfig` copied from PeckOrder, `README.md`, `LICENSE`.
  No release workflow yet.

## Approaches considered

1. **One package** with the OpenAI, Anthropic and Jev types built in. Simplest and closest to today's code, but Gizzard.Jev would
   pull in both chat SDKs and PeckOrder would pull in Jev.Net.
2. **Core plus one package per SDK.** Picked by the owner. The core depends only on abstractions; `.OpenAI`, `.Anthropic` and
   `.Jev` each add their types. Every consumer references exactly what it uses. The cost is four projects, and per-type
   validation moves out of the shared validator into each type's registration.
3. **Core with the chat types built in, Jev as an add-on.** PeckOrder's setup barely changes, but Gizzard.Jev still pulls both
   chat SDKs through the core. That breaks goal 3.

Approach 2 is the only one that meets goal 3 for every consumer.

## The design

### Packages

| Project | Contains | Depends on |
|---|---|---|
| `PinkRooster.ProviderConfig` | `ProviderOptions`, the options type holding the `Providers` dictionary, the type registry, the generic client factory, `IChatClientFactory`, `ProviderContext`, the startup validator (common rules), the HTTP test seam, `AddProviderConfig()` and its builder, and the DI registration helpers for chat clients | Microsoft.Extensions.AI.Abstractions, Microsoft.Extensions.Options(.ConfigurationExtensions), Microsoft.Extensions.DependencyInjection.Abstractions, Microsoft.Extensions.Logging.Abstractions (for the plain-HTTP key warning) |
| `PinkRooster.ProviderConfig.OpenAI` | types `openai` and `openai_responses`, `ResponsesChatClient`, `AddOpenAI()` | core, Microsoft.Extensions.AI.OpenAI |
| `PinkRooster.ProviderConfig.Anthropic` | type `anthropic`, `AddAnthropic()` | core, Anthropic |
| `PinkRooster.ProviderConfig.Jev` | type `jev`, the model-pinning `ITypeSafeClient` wrapper, `ISystemOneClientFactory`, `AddJev()`, `AddSystemOneClient(...)` | core, Jev.Net |

Each has its own test project under `tests/`.

### Configuration

There is one top-level section, `Providers`, which `AddProviderConfig()` binds from the `IConfiguration` in the container. The app
doesn't call `BindConfiguration` itself. Code can also add or override providers through the builder's `Configure(...)`.

```json
{
  "Providers": {
    "openai":  { "Type": "openai_responses", "ApiKey": "...", "Timeout": "00:02:00" },
    "claude":  { "Type": "anthropic", "ApiKey": "..." },
    "ollama":  { "Type": "openai", "Endpoint": "http://localhost:11434/v1" },
    "jev":     { "Type": "jev", "ApiKey": "..." }
  }
}
```

Provider IDs are matched ignoring case. `ProviderOptions` keeps today's properties, including `Store`, so existing PeckOrder
provider entries carry over unchanged apart from the section path.

### Registration

```csharp
services.AddProviderConfig()              // idempotent; binds "Providers", registers factories + validator, ValidateOnStart
    .AddOpenAI()                          // registers openai, openai_responses
    .AddAnthropic()                       // registers anthropic
    .AddJev()                             // registers jev
    .AddProviderType<IChatClient>("custom", context => model => ...)   // extensibility, as PeckOrder's AddProviderType today
    .AddChatClient("openai", "gpt-5-mini")                                          // unkeyed IChatClient
    .AddChatClient("openai", "gpt-5", serviceKey: GizzardServiceKeys.FrontierChatClient) // keyed
    .AddSystemOneClient("jev", "jev-1.14");                                         // ITypeSafeClient (in .Jev)
```

- `AddProviderConfig()` may be called by the app, by PeckOrder's `AddPeckOrder()` and by Gizzard.Jev, in any order. Every call
  shares one registry, options instance and validator. This is the same "find the existing instance" pattern PeckOrder uses for
  `ProviderTypeRegistry` today.
- `AddOpenAI()` / `AddAnthropic()` / `AddJev()` are idempotent. Registering the same built-in type twice is a no-op. Registering a
  *different* creator under a name that is already taken still throws, as today.
- The registration helpers resolve lazily through the factory. A missing provider fails at startup validation if the helper's
  provider ID can be checked then (builder's call: see "Left to the builder"), and otherwise when the service is first resolved.

### Factories and client kinds

The registry is generalised from "type name → chat-client creator" to "type name → creator of one client kind". The kind is
`IChatClient` or `ITypeSafeClient`, and the core stays open to others:
`AddProviderType<TClient>(string type, Func<ProviderContext, Func<string, TClient>> create)`.

- The core's generic factory builds each provider's outer creator once, lazily and thread-safely. It owns everything registered
  through `ProviderContext.RegisterDisposable` and disposes it once when the container shuts down. It disposes every object and
  drops any failure without throwing, so the container still disposes the singletons after it. This is today's
  `ChatClientFactory` behaviour as of PeckOrder commit `e242e2e`, locked in by
  `Dispose_DisposesTheRemainingClients_WhenOneThrows_AndDoesNotThrow`.
- `IChatClientFactory.GetChatClient(providerId, modelId)` keeps its current signature and error messages, apart from the section
  path.
- `ISystemOneClientFactory.GetSystemOneClient(providerId, modelId)` (in `.Jev`) returns an `ITypeSafeClient`.
- Asking for the wrong kind fails clearly. For example, requesting a chat client from provider `jev` gives an error like
  "Provider 'jev' has type 'jev', which makes System One clients, not chat clients."

### The `jev` type

- One `Jev.Net.TypeSafeClient` per provider, registered for disposal. Per model, a thin `ITypeSafeClient` wrapper fills in
  `SystemOneOptions.Model` with the model ID whenever the caller passes no options or leaves `Model` null. A model set by the
  caller still wins. `Models` and the other members delegate to the shared client. Disposing the wrapper does nothing. This
  mirrors the chat types: one SDK client per provider, cheap clients per model.
- `ApiKey` is required (validation failure if blank), because Jev.Net treats blank as unset and would fall back to
  `TYPESAFE_API_KEY`.
- `BaseUrl` is always set explicitly, to `Endpoint` or `https://api.typesafe.ai`, so `TYPESAFE_BASE_URL` never applies. Because
  every call carries a model, `TYPESAFE_DEFAULT_MODEL` never applies either.
- `Timeout` maps to `TypeSafeClientOptions.Timeout`. `LoggerFactory` comes from the container when one is registered.
  `TYPESAFE_LOG_LEVEL` is left to Jev.Net: it only adjusts the logging threshold, and redirects nothing.
- `Store` on a `jev` provider is a validation failure, as on any type other than `openai_responses`.
- The HTTP test seam applies to it like the chat types (`TypeSafeClientOptions.HttpClient` over the seam's handler, not disposed).

### Validation

The startup validator stays. The core keeps the common rules: provider ID characters, `Type` required and known (listing the
known types), `Endpoint` an absolute http(s) URL, `Timeout` > 0, and `Store` only for `openai_responses`. Each type's
registration can add rules for its own options:

- `anthropic`: ApiKey required.
- `openai` / `openai_responses`: ApiKey required when there is no Endpoint.
- `jev`: ApiKey required.

Messages keep their current wording, with `PeckOrder:Providers:{id}` becoming `Providers:{id}`.

### Plain-HTTP API key warning

Neither SDK upgrades to HTTPS, so a provider with an `ApiKey` and an `http://` `Endpoint` sends its key unencrypted (PeckOrder's
`AnthropicProviderTests` shows `Authorization: Bearer k1` going to `http://up/prefix/v1/messages`). PeckOrder warns about this
today from `MapPeckOrder`. That check moves into ProviderConfig, so every consumer gets it. This was the owner's decision.

- Same condition: a non-blank `ApiKey`, an absolute `Endpoint` with scheme `http`, and a host that is not loopback. Same Warning
  level and wording, apart from the logger category: "Upstream provider '{ProviderId}' sends its API key over plain HTTP to
  {Host}. Use https unless the network is trusted."
- It is logged once per provider when the provider settings are first validated. In hosted apps that is startup, through
  `ValidateOnStart`; in apps without a host, such as the Gizzard sample CLI, it is first use. It uses the container's
  `ILoggerFactory` when there is one, and otherwise it is silent. The exact hook is the builder's call, but it must not log
  again for later options reads.
- It applies to every type, `jev` included.
- The key itself never appears in the message, a log, or a validation error. PeckOrder's `ProviderSecretTests` already check
  this for the gateway.

### The test seam

`ProviderHttpHandler` becomes public, because PeckOrder's and Gizzard's tests use it from another assembly now. It is documented
as a test-only seam, and registered through an obvious API such as `services.AddProviderHttpHandler(handler)`. Production never
registers it.

### Behaviour carried over unchanged

- Lazy creation, once per provider.
- Disposal, once, carrying on past failures.
- Startup validation.
- Explicit endpoint and key, so the `OPENAI_*` and `ANTHROPIC_*` variables cannot redirect a client.
- `openai_responses` sends `store: false` unless configured.
- `ResponsesChatClient` turns failures the upstream reports inside a successful response, streamed or not, into exceptions.
- The Anthropic timeout semantics, as documented on `ProviderOptions.Timeout`.

PeckOrder's provider tests move into ProviderConfig's test projects alongside the code: `AmbientEnvironmentTests`,
`AnthropicProviderTests`, `ChatClientFactoryTests`, `OpenAIProviderTests`, `ResponsesProviderTests`,
`ProviderOptionsBindingTests`, `ProvidersValidatorTests` and `LiveProviderTests`. Tests that exercise the gateway end to end
(`ProviderLoopbackTests`, handler tests) stay in PeckOrder. `ProviderSecretTests` is **split**, not moved: the plain-HTTP
warning tests and "key never in a validation error" tests go to ProviderConfig, while the "key never in gateway responses or
logs" tests stay in PeckOrder. New tests cover the `jev` type, client-kind mismatch, idempotent registration, the DI helpers and
the once-only warning.

## Consumer changes

### PeckOrder

- Delete `src/PinkRooster.PeckOrder.AspNetCore/Providers/`. Reference `PinkRooster.ProviderConfig`, `.OpenAI` and `.Anthropic`
  by project path. Drop the direct `Anthropic` and `Microsoft.Extensions.AI.OpenAI` package references if nothing else in
  PeckOrder uses them.
- `AddPeckOrder()` calls `AddProviderConfig().AddOpenAI().AddAnthropic()`, so a PeckOrder app still gets the three built-in types
  without extra calls.
- `PeckOrderOptions.Providers` is removed. Providers come from the top-level `Providers` section, and code-defined providers go
  through ProviderConfig's `Configure`. This is a breaking change, which is acceptable at `0.1.0-preview.1`; record it in the
  CHANGELOG.
- `MapPeckOrder` drops its provider loop and `Log.ProviderKeyOverHttp`, because ProviderConfig logs that now. It keeps the
  gateway's own "no API key" warning.
- `PeckOrderBuilder.AddProviderType` is removed in favour of ProviderConfig's builder (my call: one place to register types).
- Update `README.md`, `docs/PinkRooster.PeckOrder.AspNetCore.md`, `samples/PinkRooster.Samples.Providers` (code + README),
  `CLAUDE.md`, `AGENTS.md`, the `ProviderModel` doc comment and `CHANGELOG.md` for the new section path and package layout. Old
  specs and plans under `docs/superpowers/` stay as historical records.
- The whole PeckOrder suite passes.

### Gizzard.Jev

- New overload `AddGizzardJev(string providerId, string modelId, Action<GizzardJevOptions>? configure = null)`. It calls
  `AddProviderConfig().AddJev()` and builds `JevDecisionClient` over
  `ISystemOneClientFactory.GetSystemOneClient(providerId, modelId)`. It does **not** register a global `ITypeSafeClient` (my
  call), so it can't collide with the app's own registrations. The existing parameterless `AddGizzardJev` stays as it is.
- Gizzard.Jev references `PinkRooster.ProviderConfig` and `.Jev` by project path. Tests are added for the overload.

### Gizzard sample CLI

- Settings come only from configuration: `appsettings.json` next to the binary, user secrets, and the standard
  environment-variable configuration provider (`Providers__openai__ApiKey` style). The `--openai-key`, `--ollama` and
  `--jev-key` options are removed, along with the bespoke `OPENAI_API_KEY` / `TYPESAFE_API_KEY` lookups. `--model` is removed too
  (my call, because the owner asked for no settings through CLI arguments); models are named in configuration.
- The hand-written `OpenAiCompatibleChatClient` is deleted. Chat and decision clients come from a container built with
  `AddProviderConfig().AddOpenAI().AddAnthropic().AddJev()`.
- The sample names which provider/model it uses in its own section (shape is my proposal):

  ```json
  "Gizzard": {
    "Chat":      { "Provider": "openai", "Model": "gpt-5-mini" },
    "Decisions": { "Provider": "jev",    "Model": "jev-1.14" }
  }
  ```

  With no `Decisions` entry, the sample falls back to the chat-based decision client, as it does today when there is no Jev key.
  A missing `Chat` entry in live mode is an error naming the section to fill in.
- `--demo` keeps working with no configuration at all.
- `ModelFactoryTests` are rewritten for the new wiring, and the sample's README/usage docs are updated.

## Left to the builder

- Exact type and member names other than the ones above (`AddProviderConfig`, `IChatClientFactory`, `ISystemOneClientFactory`,
  `AddOpenAI`/`AddAnthropic`/`AddJev`, `AddChatClient`, `AddSystemOneClient`, `ProviderHttpHandler`).
- Namespaces, provided that public API lives under `PinkRooster.ProviderConfig[.OpenAI|.Anthropic|.Jev]`.
- How a type registration declares its own validation and whether it uses `Store`.
- Whether the DI helpers' provider IDs are checked at startup validation or on first resolve.
- Whether the extension methods use C# 14 extension blocks (Gizzard does) or classic static extensions (PeckOrder does).
- Test assertion style. Follow PeckOrder's plain xunit `Assert` for moved tests.
- Order of work across the three repos, and the commits and branches in each.
