# PinkRooster.ProviderConfig Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract PeckOrder's provider layer into `PinkRooster.ProviderConfig` (core plus `.OpenAI`, `.Anthropic` and `.Jev`), add a
`jev` System One provider type, and switch PeckOrder, Gizzard.Jev and the Gizzard sample CLI over to it.

**Architecture:** The core binds a top-level `Providers` configuration section. It keeps a registry of provider types, each of
which creates one kind of client (`IChatClient` or Jev.Net's `ITypeSafeClient`), and it builds each provider's SDK client once
through a generic factory that disposes everything at shutdown. Each SDK package registers its types on the core's builder. The
consumers reference the packages by relative project path.

**Tech Stack:** .NET 10 / C# 14, Microsoft.Extensions.AI 10.11.0, OpenAI via Microsoft.Extensions.AI.OpenAI 10.11.0, Anthropic
13.1.0, Jev.Net 0.5.0, xunit v3 on Microsoft.Testing.Platform.

**Spec:** `docs/superpowers/specs/2026-10-24-provider-config-design.md`. Read it before starting; this plan argues from it.

## Global Constraints

- Three repos, side by side under `/home/najgeetsrev/Development/`: `ProviderConfig` (branch `feature/provider-config`, already
  exists), `PeckOrder` (create `feature/provider-config` from `main`), `Gizzard` (create `feature/provider-config` from `master`).
  **Work in these checkouts directly, not in git worktrees.** The consumers reference `../ProviderConfig` by relative path, and a
  worktree breaks that.
- Before Task 1, `git pull` PeckOrder `main`. Extract from its latest code, not from snippets quoted here (spec: "Extract from
  PeckOrder's latest `main` at the time the work starts").
- `net10.0`, `Nullable` enabled, `LangVersion` latest, `ImplicitUsings`, `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, and
  `GenerateDocumentationFile` for `src/` projects.
- Library style (`src/**.cs`, from PeckOrder's `.editorconfig`): explicit types (no `var`) and braces always. Tests may use `var`.
- Public API carries XML docs, as PeckOrder's does. Moved code keeps its existing comments and doc text, edited only where a
  name or path changed.
- Extension methods use C# 14 extension blocks (`extension(IServiceCollection services) { ... }`), as Gizzard does.
- Central package versions (ProviderConfig `Directory.Packages.props`): Microsoft.Extensions.AI.Abstractions / .AI.OpenAI 10.11.0;
  Anthropic 13.1.0; Jev.Net 0.5.0; Microsoft.Extensions.{Options, Options.ConfigurationExtensions, DependencyInjection.Abstractions,
  Logging.Abstractions} 10.0.12. Tests: Microsoft.Extensions.{DependencyInjection, Hosting, Configuration} 10.0.12,
  Microsoft.NET.Test.Sdk 18.10.1, xunit.v3 4.0.1, xunit.runner.visualstudio 4.0.0, Microsoft.SourceLink.GitHub 10.0.401. Don't
  go above the consumers' versions: that would raise NU1605 downgrade errors in them.
- Configuration section: `Providers`. Validation messages use the prefix `Providers:{id}`.
- No test calls a real API. Live tests skip unless their environment variables are set, as PeckOrder's `LiveProviderTests` do.
- Known and accepted: PeckOrder's and Gizzard's CI and packing break because of the cross-repo project references. Don't fix it.
- Commit after each task, with conventional-commit messages ending in
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Double registration across consumers.** An app calls `AddPeckOrder()`, then `AddProviderConfig().AddOpenAI().AddJev()`, or
   Gizzard.Jev's overload calls `AddJev()` again. Nothing should throw, and there should be one registry and one validator.
   Tests: Task 2 `AddProviderType_IsANoOp_ForTheSameRegistration`; Task 5 `AddOpenAI_Twice_IsANoOp`; Task 9
   `AddPeckOrder_ThenAddProviderConfigAgain_StartsCleanly`.
2. **No `IConfiguration` in the container** (plain `ServiceCollection`, as in Gizzard's tests and many unit tests). Code-only
   providers must work. Test: Task 1 `Providers_FromCodeOnly_WithoutAnIConfiguration`.
3. **The caller's `SystemOneOptions`.** A caller-set `Model` wins. A caller's options with `Model` null keep their `ExtraBody`,
   `ExtraHeaders`, `Retry` and `Timeout`. The caller's instance is never mutated. Tests: Task 7
   `Jev_CallersModelWins`, `Jev_KeepsTheCallersOtherOptions_AndDoesNotChangeThem`.
4. **A Jev endpoint with a path prefix and/or a trailing slash** (`http://up/prefix/`) must post to
   `http://up/prefix/v1/systemone`, with no double slash. Test: Task 7 `Jev_TrailingSlashOnTheEndpoint_IsIgnored`.
5. **Repeated options reads** (`IOptions`, `IOptionsMonitor` and `IOptionsSnapshot` in one app) must log the plain-HTTP warning
   only once per provider. Test: Task 3 `PlainHttpWarning_IsLoggedOnce_AcrossRepeatedReads`.

---

## Part A: ProviderConfig repo

### Task 1: Repo scaffold, options and configuration binding

**Files:**
- Create: `PinkRooster.ProviderConfig.slnx`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`,
  `global.json`, `.editorconfig`, `.gitignore`, `.gitattributes`, `LICENSE`, `README.md` (a stub; Task 8 completes it)
- Create: `src/PinkRooster.ProviderConfig/PinkRooster.ProviderConfig.csproj`
- Create: `src/PinkRooster.ProviderConfig/Options/ProviderOptions.cs`, `Options/ProviderConfigOptions.cs`,
  `Options/ConfigurationBinding.cs`, `Builders/ProviderConfigBuilder.cs`,
  `Extensions/ProviderConfigServiceCollectionExtensions.cs`
- Create: `tests/PinkRooster.ProviderConfig.Tests/PinkRooster.ProviderConfig.Tests.csproj`,
  `tests/PinkRooster.ProviderConfig.Tests/Options/ProviderOptionsBindingTests.cs`
- Create: `tests/Shared/` (test support shared by every test project through linked compile items:
  `<Compile Include="../Shared/**/*.cs" LinkBase="Shared" />`)

**Interfaces:**
- Produces (namespace `PinkRooster.ProviderConfig`):
  - `public sealed class ProviderOptions`: `Type`, `Endpoint`, `ApiKey`, `Timeout`, `Store`, exactly as PeckOrder's, with docs
    saying "configured under `Providers:<id>`".
  - `public sealed class ProviderConfigOptions { public const string SectionName = "Providers"; public IDictionary<string, ProviderOptions> Providers { get; } }`,
    where the dictionary uses `StringComparer.OrdinalIgnoreCase`.
  - `public sealed class ProviderConfigBuilder { public IServiceCollection Services { get; } public ProviderConfigBuilder Configure(Action<ProviderConfigOptions> configure); }`.
    Later tasks add members.
  - `extension(IServiceCollection services) { public ProviderConfigBuilder AddProviderConfig(); }` in the static class
    `ProviderConfigServiceCollectionExtensions`.

- [ ] **Step 1: Scaffold the repo.** Copy PeckOrder's `.editorconfig`, `.gitattributes`, `.gitignore` and `global.json`. Take
  `Directory.Build.props` from PeckOrder, with these changes: `PackageProjectUrl`/`RepositoryUrl`
  `https://github.com/pinkroosterai/PinkRooster.ProviderConfig`; `PackageReleaseNotes` pointing at this repo's CHANGELOG; no
  `PackageIcon` or icon item; `Version` `0.1.0-preview.1`. `Directory.Build.targets` is PeckOrder's, with the target renamed
  `PinProviderConfigSiblingVersions`. Copy PeckOrder's `LICENSE` (MIT, Jan Versteeg 2026). `Directory.Packages.props` gets the
  versions in Global Constraints. The core csproj references Microsoft.Extensions.AI.Abstractions, Options,
  Options.ConfigurationExtensions, DependencyInjection.Abstractions and Logging.Abstractions, and declares
  `InternalsVisibleTo PinkRooster.ProviderConfig.Tests`. The test csproj follows PeckOrder's test csproj (Exe, not packable,
  `NoWarn xUnit1051`, global `using Xunit`), plus DependencyInjection, Hosting and Configuration.

- [ ] **Step 2: Write the failing tests** in `ProviderOptionsBindingTests`. Port PeckOrder's four tests with the keys rewritten
  `PeckOrder:Providers:` → `Providers:`. Each test builds a `ServiceCollection` with `AddSingleton<IConfiguration>(config)` and
  `AddProviderConfig()`, then reads `IOptions<ProviderConfigOptions>`:
  `Providers_BindFromTheSection_KeyedById` (keeps the `Type`/`Endpoint`/`Timeout 00:02:00`/`Store true` assertions),
  `Providers_IgnoreCase_ForTheId`, `Providers_LaterSourceOverridesTheKey` and `ProviderOptions_ToString_DoesNotShowTheKey`.
  Add these:
  - `Providers_FromCodeOnly_WithoutAnIConfiguration`: no `IConfiguration` registered;
    `AddProviderConfig().Configure(o => o.Providers["p"] = new ProviderOptions { Type = "t" })`; `Providers["P"].Type == "t"`.
  - `Code_OverridesConfiguration_ForTheSameId`: config sets `Providers:p:Type = a`, `Configure` sets `b`; result `b`.
  - `AddProviderConfig_Twice_BindsOnce`: config `Providers:p:Type = a`; calling `AddProviderConfig()` twice still gives
    `Providers.Count == 1` and exactly one `ConfigurationBinding` registered as `IConfigureOptions<ProviderConfigOptions>`.

- [ ] **Step 3: Run the tests to make sure they fail.** Run: `dotnet test --solution PinkRooster.ProviderConfig.slnx`.
  Expected: a build failure (the types don't exist yet).

- [ ] **Step 4: Implement.** `ConfigurationBinding(IServiceProvider services) : IConfigureOptions<ProviderConfigOptions>` binds
  `services.GetService<IConfiguration>()?.GetSection(SectionName)` into `options.Providers` and does nothing when there is no
  `IConfiguration`. `AddProviderConfig()` registers `AddOptions<ProviderConfigOptions>()` and the binding with
  `TryAddEnumerable`, so it binds once and before any later `Configure`. It returns `new ProviderConfigBuilder(services, ...)`.
  `Configure` forwards to `services.Configure(configure)`.

- [ ] **Step 5: Run the tests.** Expected: all pass, and the build shows 0 warnings.

- [ ] **Step 6: Commit.** `feat(core): bind providers from the Providers section`

### Task 2: Type registry, generic factory, chat client factory, test seam

**Files:**
- Create: `src/PinkRooster.ProviderConfig/Registry/ProviderTypeRegistry.cs`, `Registry/ProviderTypeOptions.cs`,
  `Factories/IProviderClientFactory.cs`, `Factories/IChatClientFactory.cs`, `Factories/ProviderClientFactory.cs`,
  `Models/ProviderContext.cs`, `Testing/ProviderHttpHandler.cs`
- Modify: `Builders/ProviderConfigBuilder.cs`, `Extensions/ProviderConfigServiceCollectionExtensions.cs`
- Test: `tests/PinkRooster.ProviderConfig.Tests/Factories/ProviderClientFactoryTests.cs`, and the shared fakes it needs in
  `tests/Shared/`

**Interfaces:**
- Consumes: Task 1.
- Produces:
  - `public sealed class ProviderTypeOptions { public Func<ProviderOptions, IEnumerable<string>>? Validate { get; init; } public bool UsesStore { get; init; } }`.
    `Validate` returns messages *without* the `Providers:{id}:` prefix.
  - `ProviderConfigBuilder.AddProviderType<TClient>(string type, Func<ProviderContext, Func<string, TClient>> create, ProviderTypeOptions? options = null) where TClient : class`,
    returning the builder.
  - `public interface IProviderClientFactory { TClient GetClient<TClient>(string providerId, string modelId) where TClient : class; }`
  - `public interface IChatClientFactory { IChatClient GetChatClient(string providerId, string modelId); }`, with PeckOrder's doc
    text (`Providers` instead of `PeckOrder:Providers`).
  - `public sealed class ProviderContext`: PeckOrder's, with the same `internal` constructor and the same members
    (`ProviderId`, `Options`, `Services`, `RegisterDisposable`).
  - `public sealed class ProviderHttpHandler(HttpMessageHandler handler) { public HttpClient CreateClient(); }`, with PeckOrder's
    behaviour and its doc text saying it is a test-only seam.
  - Internal: `ProviderTypeRegistry` with `Types` (sorted names), `TryGet(string, out ProviderTypeRegistration)`,
    `Add(ProviderTypeRegistration)`, and `ProviderTypeRegistration(string Name, Type ClientType, Delegate Create, ProviderTypeOptions Options)`.
    `ProviderClientFactory : IProviderClientFactory, IChatClientFactory, IDisposable`.

- [ ] **Step 1: Write the failing tests.** Port PeckOrder's `ChatClientFactoryTests`, except
  `UnknownProvider_IsAGeneric500_WithoutTheProviderId`, which stays in PeckOrder (Task 9). Each test uses
  `AddProviderConfig().AddProviderType<IChatClient>(...)` instead of `AddPeckOrder().AddProviderType(...)`. Keep the error
  messages, apart from these. Unknown provider:
  `"No upstream provider 'x' is configured. Configured providers: (none)."`. Duplicate type: `ArgumentException`
  `"A provider type 'fake' is already registered."` with `ParamName == "type"`. Keep
  `Dispose_DisposesTheRemainingClients_WhenOneThrows_AndDoesNotThrow` exactly as it is. Add these:
  - `GetClient_Throws_ForTheWrongClientKind`: register `"sys"` as `AddProviderType<FakeSystemOneClient>`. Then
    `GetChatClient("p", "m")` throws `InvalidOperationException`
    `"Upstream provider 'p' has type 'sys', which creates FakeSystemOneClient, not IChatClient."`, and the outer creator never
    ran.
  - `AddProviderType_IsANoOp_ForTheSameRegistration`: the same name, the same `TClient` and the same static method group, added
    twice, doesn't throw.
  - `AddProviderType_Throws_ForTheSameNameWithAnotherClientKind`.
  - `AddProviderConfig_Twice_SharesOneRegistry`: a type added through the first builder is usable through the second.
  - `ChatClientFactory_AndProviderClientFactory_AreOneInstance`.

- [ ] **Step 2: Run them.** Run: `dotnet test --solution PinkRooster.ProviderConfig.slnx`. Expected: build failure.

- [ ] **Step 3: Implement.** Move PeckOrder's `ChatClientFactory` logic into `ProviderClientFactory`. The lazy creator per
  provider becomes a `Lazy<Delegate>`. Check the kind *before* creating the lazy (`typeof(TClient) == registration.ClientType`),
  then cast to `Func<string, TClient>`. `IOptions<ProviderConfigOptions>` replaces `PeckOrderOptions`. `GetChatClient` forwards
  to `GetClient<IChatClient>`. Registry `Add` rules: if the name is free, add it. If the same name already has an equal
  registration (same `ClientType`, and `Delegate.Equals` on `Create`), it's a no-op. Anything else throws. Find the existing
  registry the way PeckOrder does (the last `ImplementationInstance` of that service type). Register
  `TryAddSingleton<ProviderClientFactory>`, then `TryAddSingleton<IProviderClientFactory>` and `TryAddSingleton<IChatClientFactory>`
  forwarding to it. Its `Dispose` is idempotent, so a forwarding registration being disposed twice is harmless.

- [ ] **Step 4: Run the tests.** Expected: all pass.

- [ ] **Step 5: Commit.** `feat(core): provider type registry and client factory with client kinds`

### Task 3: Startup validation and the plain-HTTP key warning

**Files:**
- Create: `src/PinkRooster.ProviderConfig/Validation/ProvidersValidator.cs`, `Validation/Log.cs`
- Modify: `Extensions/ProviderConfigServiceCollectionExtensions.cs` (register the validator with
  `TryAddEnumerable(Singleton<IValidateOptions<ProviderConfigOptions>, ProvidersValidator>)`, plus `ValidateOnStart()`)
- Test: `tests/PinkRooster.ProviderConfig.Tests/Validation/ProvidersValidatorTests.cs`, `Validation/PlainHttpWarningTests.cs`,
  and a capturing `ILoggerProvider` in `tests/Shared/`

**Interfaces:**
- Consumes: the registry from Task 2 (`Types`, `TryGet`, `ProviderTypeOptions.Validate`/`UsesStore`).
- Produces: failure messages that Tasks 5–7 and the consumers assert on.

- [ ] **Step 1: Write the failing validator tests.** Register two test types: `"fake"` (no options) and `"needs_key"`
  (`Validate` = blank key → `"ApiKey is required for type 'needs_key'."`, `UsesStore = true`). Then `Known = "fake, needs_key"`.
  A `[Theory]` resolves `IOptions<ProviderConfigOptions>.Value` and expects an `OptionsValidationException` containing exactly
  these messages:
  - `"Provider ID 'bad id' may only contain letters, digits, '_' and '-'."`
  - `"Providers:p:Type is required. Known types: {Known}."` (for null and for blank)
  - `"Providers:p:Type 'nope' is not a known provider type. Known types: {Known}."`
  - `"Providers:p:Endpoint must be an absolute http or https URL."` (relative, `ftp://x/`)
  - `"Providers:p:Timeout must be greater than zero."` (zero, −1 s)
  - `"Providers:p:ApiKey is required for type 'needs_key'."` (from the type's `Validate`, with the prefix added by the core)
  - `"Providers:p:Store applies only to type 'needs_key'."` (`Store` set on `fake`)
  - `"Providers:p is empty."` (null entry)

  Add `Store_OnAnyType_WhenNoTypeUsesStore` (only `fake` registered): `"Providers:p:Store is not used by type 'fake'."` Port
  `Start_ReportsEveryFailure_AtOnce`, `Start_Succeeds_ForProvidersSetInCode`, `Start_Succeeds_ForACustomType_WithoutAKey` and
  `Start_Succeeds_WithNoProviders`. Port `Start_Fails_ForAnInvalidProvider` with `Host.CreateApplicationBuilder()`, to prove
  `ValidateOnStart` makes `StartAsync` throw. Add `TheKey_NeverAppearsInValidationErrors`: with the key `"sentinel-key-123"` on
  every invalid provider above, no failure message contains it.

- [ ] **Step 2: Write the failing warning tests** (the capturing logger is registered as an `ILoggerProvider`):
  - `PlainHttpToARemoteHost_LogsOneWarning`: provider `remote`, type `fake`, `ApiKey = "any-key"`, `Endpoint = http://10.0.0.5/v1`.
    Exactly one Warning:
    `"Upstream provider 'remote' sends its API key over plain HTTP to 10.0.0.5. Use https unless the network is trusted."`
  - `PlainHttpToLoopbackOrHttps_LogsNothing`, `[Theory]` over `http://127.0.0.1:11434/v1`, `http://localhost/v1` and
    `https://api.example.com/v1`.
  - `PlainHttp_WithoutAKey_LogsNothing`.
  - `PlainHttpWarning_IsLoggedOnce_AcrossRepeatedReads`: read `IOptions<>.Value`, `IOptionsMonitor<>.CurrentValue`, and
    `IOptionsSnapshot<>.Value` from two scopes. Exactly one warning.
  - `NoLoggerFactory_DoesNotThrow`.

- [ ] **Step 3: Run them.** Expected: they fail. The validator isn't registered yet, so no exception is thrown and no warning is
  logged.

- [ ] **Step 4: Implement `ProvidersValidator(ProviderTypeRegistry types, IServiceProvider services) : IValidateOptions<ProviderConfigOptions>`.**
  Port PeckOrder's rules in this order, minus `MaxRequestBodyBytes` and minus the hard-coded per-type key rules: ID characters,
  `Type` required and known, `Endpoint`, then (only for a known type) the type's `Validate` messages, prefixed. Then `Timeout`,
  then `Store`:
  - Store set and the type isn't `UsesStore`, with at least one registered `UsesStore` type: `"{prefix}:Store applies only to type 'a'."`.
    With several such types, `"{prefix}:Store applies only to types 'a', 'b'."`.
  - With none registered: `"{prefix}:Store is not used by type '{type}'."`.

  After the checks, on the first `Validate` call only (an `Interlocked` flag), log the plain-HTTP warning for each matching
  provider. The condition is PeckOrder's `MapPeckOrder` loop: non-blank key, absolute `http`, not loopback. Use
  `services.GetService<ILoggerFactory>()?.CreateLogger("PinkRooster.ProviderConfig")`. `Log.cs` is a `[LoggerMessage]` partial,
  with `Level = Warning` and the exact message above.

- [ ] **Step 5: Run the tests.** Expected: all pass.

- [ ] **Step 6: Commit.** `feat(core): validate providers at startup and warn about keys over plain HTTP`

### Task 4: DI registration helper for chat clients

**Files:**
- Modify: `Builders/ProviderConfigBuilder.cs`
- Test: `tests/PinkRooster.ProviderConfig.Tests/Builders/AddChatClientTests.cs`

**Interfaces:**
- Produces: `ProviderConfigBuilder.AddChatClient(string providerId, string modelId, object? serviceKey = null)`. With a null key
  it registers an unkeyed singleton `IChatClient`; otherwise a keyed singleton. Both resolve through
  `IChatClientFactory.GetChatClient(providerId, modelId)` the first time they're resolved. Task 7 mirrors this pattern for
  `AddSystemOneClient`.

- [ ] **Step 1: Write the failing tests:**
  - `AddChatClient_RegistersAnUnkeyedClient_ForTheModel`: a fake type records the model it was asked for; it was `"m1"`.
  - `AddChatClient_RegistersAKeyedClient`: `GetRequiredKeyedService<IChatClient>("fast")` works, and no unkeyed client is
    registered.
  - `AddChatClient_IsLazy`: building the provider doesn't run the type's creator.
  - `AddChatClient_ForAnUnknownProvider_FailsOnResolve`: the factory's unknown-provider message.
  - `AddChatClient_Throws_ForBlankArguments`: `ArgumentException`.

- [ ] **Step 2: Run them.** Expected: build failure.

- [ ] **Step 3: Implement** with `TryAdd`-free `AddSingleton` / `AddKeyedSingleton`, so an app can deliberately register
  several.

- [ ] **Step 4: Run the tests.** Expected: all pass.

- [ ] **Step 5: Commit.** `feat(core): register a configured model as a chat client service`

### Task 5: `PinkRooster.ProviderConfig.OpenAI`

**Files:**
- Create: `src/PinkRooster.ProviderConfig.OpenAI/PinkRooster.ProviderConfig.OpenAI.csproj` (references the core and
  Microsoft.Extensions.AI.OpenAI; `InternalsVisibleTo` its tests), `OpenAIProviderType.cs`, `ResponsesChatClient.cs`,
  `Extensions/OpenAIProviderConfigBuilderExtensions.cs`
- Create: `tests/PinkRooster.ProviderConfig.OpenAI.Tests/` (its csproj adds `NoWarn OPENAI001`; move PeckOrder's `StubUpstream`
  into `tests/Shared/`), with `OpenAIProviderTests.cs`, `ResponsesProviderTests.cs`, `AmbientEnvironmentTests.cs`,
  `LiveProviderTests.cs` and `RegistrationTests.cs`
- Modify: `PinkRooster.ProviderConfig.slnx`

**Interfaces:**
- Consumes: `ProviderConfigBuilder.AddProviderType<IChatClient>`, `ProviderTypeOptions`, `ProviderContext`,
  `ProviderHttpHandler`.
- Produces: `extension(ProviderConfigBuilder builder) { public ProviderConfigBuilder AddOpenAI(); }` in namespace
  `PinkRooster.ProviderConfig.OpenAI`. It registers `openai` (Chat Completions) and `openai_responses` (Responses,
  `UsesStore = true`). Both have `Validate`: blank key and no `Endpoint` → `"ApiKey is required for type '{type}' without an Endpoint."`.

- [ ] **Step 1: Write the failing tests.** Port PeckOrder's `OpenAIProviderTests` (all 8). Port `ResponsesProviderTests`
  without the two `Gateway_*` tests, which stay in PeckOrder. Port the OpenAI half of `AmbientEnvironmentTests` (the
  `OPENAI_API_KEY`/`OPENAI_BASE_URL` variables, same `DisableParallelization` collection). Port the two OpenAI cases of
  `LiveProviderTests`, with the same environment-variable gating as in PeckOrder. They build with
  `AddProviderConfig().AddOpenAI()` and `services.AddSingleton(new ProviderHttpHandler(stub))`. The placeholder key assertion
  becomes `"Bearer providerconfig-no-key"`. Add `RegistrationTests`:
  - `AddOpenAI_RegistersBothTypes`: a provider of each type validates.
  - `AddOpenAI_Twice_IsANoOp`.
  - `OpenAI_ApiKeyRequired_WithoutAnEndpoint`, `[Theory]` over both types, expecting the message above with the
    `Providers:p:` prefix.
  - `Store_IsRejected_OnOpenAIChatCompletions`: `"Providers:p:Store applies only to type 'openai_responses'."`.

- [ ] **Step 2: Run them.** Expected: build failure.

- [ ] **Step 3: Move `OpenAIProviderType` and `ResponsesChatClient`** from PeckOrder unchanged, apart from the namespace and
  `PlaceholderKey = "providerconfig-no-key"`. Implement `AddOpenAI()`.

- [ ] **Step 4: Run the tests.** Expected: all pass.

- [ ] **Step 5: Commit.** `feat(openai): openai and openai_responses provider types`

### Task 6: `PinkRooster.ProviderConfig.Anthropic`

**Files:**
- Create: `src/PinkRooster.ProviderConfig.Anthropic/` (csproj referencing the core and Anthropic; `AnthropicProviderType.cs`;
  `Extensions/AnthropicProviderConfigBuilderExtensions.cs`)
- Create: `tests/PinkRooster.ProviderConfig.Anthropic.Tests/` with `AnthropicProviderTests.cs`, `AmbientEnvironmentTests.cs`,
  `LiveProviderTests.cs` and `RegistrationTests.cs`
- Modify: `PinkRooster.ProviderConfig.slnx`

**Interfaces:**
- Produces: `extension(ProviderConfigBuilder builder) { public ProviderConfigBuilder AddAnthropic(); }` in namespace
  `PinkRooster.ProviderConfig.Anthropic`. It registers `anthropic` with `Validate`: blank key → `"ApiKey is required for type 'anthropic'."`.

- [ ] **Step 1: Write the failing tests.** Port PeckOrder's `AnthropicProviderTests` (4), the Anthropic half of
  `AmbientEnvironmentTests` (`ANTHROPIC_API_KEY`, `ANTHROPIC_AUTH_TOKEN`, `ANTHROPIC_BASE_URL`) and the Anthropic live test. Add
  these:
  - `AddAnthropic_Twice_IsANoOp`.
  - `Anthropic_ApiKeyRequired`, `[Theory]` over null and `"  "`: `"Providers:p:ApiKey is required for type 'anthropic'."`.

- [ ] **Step 2: Run them.** Expected: build failure.

- [ ] **Step 3: Move `AnthropicProviderType`** unchanged, apart from the namespace. Implement `AddAnthropic()`.

- [ ] **Step 4: Run the tests.** Expected: all pass.

- [ ] **Step 5: Commit.** `feat(anthropic): anthropic provider type`

### Task 7: `PinkRooster.ProviderConfig.Jev`

**Files:**
- Create: `src/PinkRooster.ProviderConfig.Jev/` (csproj referencing the core and Jev.Net; `JevProviderType.cs`;
  `ModelPinnedTypeSafeClient.cs`; `ISystemOneClientFactory.cs`; `SystemOneClientFactory.cs`;
  `Extensions/JevProviderConfigBuilderExtensions.cs`)
- Create: `tests/PinkRooster.ProviderConfig.Jev.Tests/` with `JevProviderTests.cs`, `AmbientEnvironmentTests.cs`,
  `LiveProviderTests.cs` and `RegistrationTests.cs`
- Modify: `PinkRooster.ProviderConfig.slnx`

**Interfaces:**
- Consumes: `AddProviderType<ITypeSafeClient>`, `IProviderClientFactory`, `ProviderHttpHandler`, and the `AddChatClient`
  pattern from Task 4.
- Produces (namespace `PinkRooster.ProviderConfig.Jev`):
  - `public interface ISystemOneClientFactory { Jev.Net.ITypeSafeClient GetSystemOneClient(string providerId, string modelId); }`
  - `extension(ProviderConfigBuilder builder) { public ProviderConfigBuilder AddJev(); public ProviderConfigBuilder AddSystemOneClient(string providerId, string modelId, object? serviceKey = null); }`.
    `AddJev` registers type `jev` (`Validate`: blank key → `"ApiKey is required for type 'jev'."`) and
    `TryAddSingleton<ISystemOneClientFactory, SystemOneClientFactory>`.

Facts about Jev.Net 0.5.0 (checked against the package): `ITypeSafeClient` has `Models` plus four `SystemOneAsync` overloads,
and is **not** `IDisposable`. `SystemOneOptions` is a sealed class, not a record, with settable `Model`, `ExtraBody`, `Retry`,
`Timeout` and `ExtraHeaders`. Requests are `POST {BaseUrl}/v1/systemone` with `Authorization: Bearer <key>` and `"model"` in the
JSON body. `TypeSafeClientOptions` has `ApiKey`, `BaseUrl` (string), `Timeout` (`TimeSpan?`), `HttpClient`,
`DisposeHttpClient` and `LoggerFactory`. A blank `ApiKey` or `BaseUrl` falls back to `TYPESAFE_API_KEY` /
`TYPESAFE_BASE_URL`. For a canned response body, copy the shape of Gizzard's `tests/PinkRooster.Gizzard.Jev.Tests/Fakes/JevResponses.cs`
(`model`, `answers`, `usage`).

- [ ] **Step 1: Write the failing tests** (stub through `ProviderHttpHandler`; provider `jev`, `ApiKey = "k1"`):
  - `Jev_PostsSystemOne_WithKeyAndModel`: `GetSystemOneClient("jev", "jev-1.14").SystemOneAsync(...)` posts to
    `https://api.typesafe.ai/v1/systemone` with `Authorization: Bearer k1` and body `model == "jev-1.14"`.
  - `Jev_UsesTheEndpoint_WithItsPath`: `Endpoint = http://up/prefix` → `http://up/prefix/v1/systemone`.
  - `Jev_TrailingSlashOnTheEndpoint_IsIgnored`: `http://up/prefix/` → `http://up/prefix/v1/systemone`.
  - `Jev_CallersModelWins`: options `Model = "jev-other"` → body model `jev-other`.
  - `Jev_KeepsTheCallersOtherOptions_AndDoesNotChangeThem`: options with `Model = null` and `ExtraBody = { "x": 1 }` → body has
    `x == 1` and `model == "jev-1.14"`; afterwards the caller's `options.Model` is still null.
  - `Jev_GenericOverloads_AlsoPinTheModel`, covering `SystemOneAsync<TResponse>` with no extra arguments, with
    `JsonTypeInfo<TResponse>`, and with `JsonSerializerOptions`.
  - `Jev_OneSdkClientPerProvider`: two models share one underlying client (the outer creator runs once).
  - `Jev_GetSystemOneClient_MakesNoRequest`.
  - `Jev_Timeout_IsPassedToTheSdk`: a hanging stub plus `Timeout = 00:00:00.2` → a `TypeSafeApiTimeoutException`.
  - `Jev_IsDisposedWithTheContainer`: after the provider is disposed, a call throws `ObjectDisposedException` (or whatever
    Jev.Net throws after disposal; assert the type it really throws).
  - `GetChatClient_OnAJevProvider_FailsWithTheKindMessage`:
    `"Upstream provider 'jev' has type 'jev', which creates ITypeSafeClient, not IChatClient."`.
  - `AddSystemOneClient_RegistersUnkeyedAndKeyed_PinnedClients`.
  - `AmbientEnvironmentTests` (non-parallel collection) sets `TYPESAFE_API_KEY=ambient`, `TYPESAFE_BASE_URL=http://ambient` and
    `TYPESAFE_DEFAULT_MODEL=ambient-model`. Requests still go to the configured endpoint with `Bearer k1` and `jev-1.14`.
  - `RegistrationTests`: `AddJev_Twice_IsANoOp`; `Jev_ApiKeyRequired` (`[Theory]` null and `"  "`):
    `"Providers:p:ApiKey is required for type 'jev'."`; `Store_IsRejected_OnJev`; and
    `PlainHttpToARemoteHost_Warns_ForJev`, with the Task 3 message.
  - `LiveProviderTests.Jev_AnswersANoul`, gated on `TYPESAFE_LIVE_TESTS=1` and `TYPESAFE_API_KEY`, as Gizzard's live tests are.

- [ ] **Step 2: Run them.** Expected: build failure.

- [ ] **Step 3: Implement `JevProviderType.Create(ProviderContext context) : Func<string, ITypeSafeClient>`.** It builds one
  `TypeSafeClient` with these settings:
  - `ApiKey = options.ApiKey`
  - `BaseUrl = options.Endpoint?.ToString().TrimEnd('/') ?? "https://api.typesafe.ai"`
  - `Timeout = options.Timeout`
  - `LoggerFactory = services.GetService<ILoggerFactory>()`
  - when the seam is registered: `HttpClient = seam.CreateClient()` and `DisposeHttpClient = false`, with the `HttpClient`
    registered for disposal

  Register the client for disposal, and return `model => new ModelPinnedTypeSafeClient(client, model)`. The wrapper's rule for
  every overload: null options → `new SystemOneOptions { Model = model }`. Options with `Model` null → a new instance copying
  all five properties, with `Model = model`. Anything else is passed through unchanged. `Models` delegates.
  `SystemOneClientFactory` forwards to `IProviderClientFactory.GetClient<ITypeSafeClient>`.

- [ ] **Step 4: Run the tests.** Expected: all pass.

- [ ] **Step 5: Commit.** `feat(jev): jev System One provider type`

### Task 8: ProviderConfig README and full verification

**Files:**
- Modify: `README.md`
- Create: `CHANGELOG.md` (`0.1.0-preview.1`: initial extraction from PeckOrder; the `jev` type)

- [ ] **Step 1: Write the README.** Cover the four packages and what each pulls in. Show a `Providers` JSON example covering
  every type, and the registration example from the spec's "Registration" section. Then the type table: type, settings, and
  which settings are required. Then the rules: ambient variables never apply; the plain-HTTP warning; the test seam
  (`services.AddSingleton(new ProviderHttpHandler(handler))`); custom types through `AddProviderType<TClient>`.

- [ ] **Step 2: Verify.** Run: `dotnet build PinkRooster.ProviderConfig.slnx -c Release && dotnet test --solution PinkRooster.ProviderConfig.slnx -c Release`
  and `dotnet pack PinkRooster.ProviderConfig.slnx -c Release -o artifacts`. Expected: 0 warnings, all tests pass (live tests
  skipped), and four `.nupkg` files. The core's nuspec must have no OpenAI, Anthropic or Jev.Net dependency. Check it with
  `unzip -p artifacts/PinkRooster.ProviderConfig.0.1.0-preview.1.nupkg '*.nuspec'`. Delete `artifacts/` afterwards.

- [ ] **Step 3: Commit.** `docs: README and changelog for ProviderConfig`

## Part B: PeckOrder repo

### Task 9: Switch PeckOrder to ProviderConfig

**Files:**
- Delete: `src/PinkRooster.PeckOrder.AspNetCore/Providers/` (the whole folder)
- Modify: `src/PinkRooster.PeckOrder.AspNetCore/PinkRooster.PeckOrder.AspNetCore.csproj`. Add project references to
  `..\..\..\ProviderConfig\src\PinkRooster.ProviderConfig.OpenAI\PinkRooster.ProviderConfig.OpenAI.csproj` and `...Anthropic...`.
  Drop the `Anthropic` and `Microsoft.Extensions.AI.OpenAI` package references if `grep -rn "using OpenAI\|using Anthropic" src`
  finds nothing else. Prune `Directory.Packages.props` to match, keeping any version a test project still uses.
- Modify: `Extensions/PeckOrderServiceCollectionExtensions.cs`. `AddPeckOrder` calls
  `services.AddProviderConfig().AddOpenAI().AddAnthropic()` in place of its own factory/registry/validator registration.
- Create: `src/PinkRooster.PeckOrder.AspNetCore/Options/PeckOrderOptionsValidator.cs`. It keeps only
  `"PeckOrder:MaxRequestBodyBytes must be greater than zero."`.
- Modify: `Options/PeckOrderOptions.cs` (remove `Providers`); `Builders/PeckOrderBuilder.cs` (remove the `ProviderTypeRegistry`
  constructor parameter and `AddProviderType`); `Extensions/PeckOrderEndpointRouteBuilderExtensions.cs` (remove the provider
  loop); `Endpoints/Log.cs` (remove `ProviderKeyOverHttp`); `Reasoning/Models/ProviderModel.cs` (doc: `Providers`); every
  `using PinkRooster.PeckOrder.AspNetCore.Providers` → `using PinkRooster.ProviderConfig`.
- Modify: `samples/PinkRooster.Samples.{Providers,Reasoning,Tools}/Program.cs`. Code-defined providers go through
  `builder.Services.AddProviderConfig().Configure(o => o.Providers["self"] = ...)`.
- Tests (`tests/PinkRooster.PeckOrder.AspNetCore.Tests/Providers/`): delete `AnthropicProviderTests`, `OpenAIProviderTests`,
  `AmbientEnvironmentTests`, `ProviderOptionsBindingTests`, `LiveProviderTests`, `ChatClientFactoryTests` and
  `ResponsesProviderTests`. Create `ProviderGatewayTests.cs` holding the three gateway tests kept from those files:
  `UnknownProvider_IsAGeneric500_WithoutTheProviderId`, `Gateway_SendsOneErrorEvent_ThenDone_WhenTheUpstreamStreamFails` and
  `Gateway_Returns500ServerError_WhenAWholeResponseFailed`. Replace `ProvidersValidatorTests` with
  `Options/PeckOrderOptionsValidatorTests.cs` (the `MaxRequestBodyBytes` test). In `ProviderSecretTests`, delete
  `PlainHttpToLoopbackOrHttps_LogsNothing` (now in ProviderConfig) and keep `PlainHttpToARemoteHost_LogsOneWarning` (proves the
  gateway still warns) and `TheKey_NeverAppearsInLogsOrErrors`. Update `ProviderLoopbackTests`, `TestSupport/*` and handler
  tests for the new namespace and `Providers:` config keys.

**Interfaces:**
- Consumes: `AddProviderConfig`, `ProviderConfigBuilder.Configure`, `AddOpenAI`, `AddAnthropic`, `IChatClientFactory`,
  `ProviderOptions` and `ProviderHttpHandler` from Part A.

- [ ] **Step 1: Branch.** `git -C ../PeckOrder checkout main && git pull && git checkout -b feature/provider-config`.

- [ ] **Step 2: Write the failing test.** Add `AddPeckOrder_ThenAddProviderConfigAgain_StartsCleanly` to
  `ProviderGatewayTests`. Use `Gateway.StartWithAsync`, calling `AddPeckOrder()` and then
  `AddProviderConfig().AddOpenAI().AddAnthropic().Configure(o => o.Providers["p"] = new ProviderOptions { Type = "openai", ApiKey = "k" })`.
  The gateway should start, and `IChatClientFactory.GetChatClient("p", "m")` should return a client.

- [ ] **Step 3: Run it.** Run: `dotnet test --solution PinkRooster.PeckOrder.slnx`. Expected: build failure (the namespace and
  `AddProviderConfig` are missing).

- [ ] **Step 4: Make the changes listed under Files.**

- [ ] **Step 5: Verify.** Run: `dotnet build PinkRooster.PeckOrder.slnx -c Release && dotnet test --solution PinkRooster.PeckOrder.slnx -c Release`.
  Expected: 0 warnings and all tests pass, including `PinkRooster.Samples.Tests`. Then
  `grep -rn "PeckOrder:Providers\|PeckOrder.AspNetCore.Providers" src samples tests` should find nothing.

- [ ] **Step 6: Commit.** `refactor(aspnetcore)!: take upstream providers from PinkRooster.ProviderConfig`

### Task 10: PeckOrder docs

**Files:**
- Modify: `README.md`, `docs/PinkRooster.PeckOrder.AspNetCore.md`, `samples/PinkRooster.Samples.Providers/README.md`,
  `CLAUDE.md`, `AGENTS.md`, `CHANGELOG.md`

- [ ] **Step 1: Update every mention.** `PeckOrder:Providers` → `Providers`. Note that providers and provider types now come
  from PinkRooster.ProviderConfig (`AddProviderConfig().AddProviderType<IChatClient>(...)` replaces
  `PeckOrderBuilder.AddProviderType`). The wire-test note now points at the public `PinkRooster.ProviderConfig.ProviderHttpHandler`.
  The plain-HTTP warning is now logged by ProviderConfig at validation time. In `CHANGELOG.md`, add a **Breaking** entry under
  the top unreleased section covering: the section path, `PeckOrderOptions.Providers` removed, `PeckOrderBuilder.AddProviderType`
  removed, the namespace move, and the keyless placeholder key now `providerconfig-no-key`. Leave `docs/superpowers/` alone.

- [ ] **Step 2: Verify.** `grep -rn "PeckOrder:Providers" --include=*.md . | grep -v docs/superpowers` should find only
  historical CHANGELOG lines for released versions, if there are any.

- [ ] **Step 3: Commit.** `docs: providers now come from PinkRooster.ProviderConfig`

## Part C: Gizzard repo

### Task 11: `AddGizzardJev(providerId, modelId)` overload

**Files:**
- Modify: `src/PinkRooster.Gizzard.Jev/PinkRooster.Gizzard.Jev.csproj` (project reference
  `../../../ProviderConfig/src/PinkRooster.ProviderConfig.Jev/PinkRooster.ProviderConfig.Jev.csproj`)
- Modify: `src/PinkRooster.Gizzard.Jev/Extensions/GizzardJevServiceCollectionExtensions.cs`
- Test: `tests/PinkRooster.Gizzard.Jev.Tests/Extensions/GizzardJevServiceCollectionExtensionsTests.cs`

**Interfaces:**
- Consumes: `AddProviderConfig`, `AddJev`, `ISystemOneClientFactory`, `ProviderHttpHandler`.
- Produces: `public IServiceCollection AddGizzardJev(string providerId, string modelId, Action<GizzardJevOptions>? configure = null)`,
  in the existing extension block. It validates the arguments with `ArgumentException.ThrowIfNullOrWhiteSpace` and calls
  `services.AddProviderConfig().AddJev()`. Its options are `new GizzardJevOptions { Model = modelId }`, then `configure`. Then
  `services.Replace(ServiceDescriptor.Singleton<IDecisionClient>(sp => new JevDecisionClient(sp.GetRequiredService<ISystemOneClientFactory>().GetSystemOneClient(providerId, modelId), options)))`.
  It registers no `ITypeSafeClient` and no `GizzardJevOptions` service.

- [ ] **Step 1: Branch.** `git -C ../Gizzard checkout master && git checkout -b feature/provider-config`.

- [ ] **Step 2: Write the failing tests.** These use `StubTypeSafeHandler` through `new ProviderHttpHandler(stub)`, and the
  provider `jev` set in code with `ApiKey = "k1"`. Build the `DecisionRequest` the way `JevDecisionClientTests` does.
  - `Provider_overload_answers_through_the_configured_provider`: `DecideAsync` succeeds. The stub saw body `model == "jev-1.14"`
    and the header `Authorization: Bearer k1`.
  - `Provider_overload_wins_when_registered_before_AddGizzard`, and `..._after_AddGizzard`: `IDecisionClient` is a
    `JevDecisionClient`.
  - `Provider_overload_registers_no_typesafe_client`: `GetService<ITypeSafeClient>()` is null.
  - `Provider_overload_with_an_unknown_provider_fails_on_resolve`: an `InvalidOperationException` whose message contains
    `"No upstream provider 'missing' is configured."`.

- [ ] **Step 3: Run them.** Run: `dotnet test --solution PinkRooster.Gizzard.slnx`. Expected: build failure.

- [ ] **Step 4: Implement the overload** as specified under Interfaces. Update the class's `<remarks>` to describe both
  overloads.

- [ ] **Step 5: Run the tests.** Expected: all Gizzard tests pass, with 0 warnings.

- [ ] **Step 6: Commit.** `feat(jev): AddGizzardJev overload that takes a ProviderConfig provider and model`

### Task 12: Sample CLI on configuration

**Files:**
- Delete: `samples/PinkRooster.Gizzard.SampleCli/Commands/ModelSettings.cs`, `Infrastructure/ModelFactory.cs` (including
  `OpenAiCompatibleChatClient`), `tests/PinkRooster.Gizzard.SampleCli.Tests/ModelFactoryTests.cs`
- Create: `samples/PinkRooster.Gizzard.SampleCli/Infrastructure/ModelServices.cs`, `Infrastructure/SampleConfiguration.cs`,
  `appsettings.json` (`{ "Providers": {}, "Gizzard": {} }`, copied to output with `PreserveNewest`)
- Create: `tests/PinkRooster.Gizzard.SampleCli.Tests/ModelServicesTests.cs`
- Modify: `RunSettings.cs` and `DecomposeSettings.cs` (inherit `CommandSettings`), `RunCommand.cs` and `DecomposeCommand.cs`
  (use `ModelServices`), and the sample csproj. The csproj gets `UserSecretsId` `pinkrooster-gizzard-samplecli`; package
  references Microsoft.Extensions.Configuration.Json / .UserSecrets / .EnvironmentVariables (version 10.0.13, added to Gizzard's
  `Directory.Packages.props`); and project references to ProviderConfig `.OpenAI` and `.Anthropic`
  (`../../../ProviderConfig/src/...`).
- Modify: every `*.md` outside `docs/superpowers` and `docs/research` that mentions `--openai-key`, `--ollama`, `--jev-key`,
  `--model`, `OPENAI_API_KEY`, `TYPESAFE_API_KEY` or `ModelFactory` (find them with `grep -rln`). Replace those passages with
  the configuration instructions below.

**Interfaces:**
- Consumes: `AddProviderConfig`, `AddOpenAI`, `AddAnthropic`, `AddJev`, `AddChatClient`, and `AddGizzardJev(providerId, modelId)`
  from Task 11.
- Produces:
  - `public static class SampleConfiguration { public static IConfiguration Load(); }`. It loads `appsettings.json` from
    `AppContext.BaseDirectory` (optional), user secrets (optional), then environment variables.
  - `public sealed class ModelServices : IDisposable { public IChatClient Chat { get; } public IDecisionClient Decisions { get; } public static ModelServices Create(IConfiguration configuration, bool demoMode, bool failGate = false); public void Dispose(); }`.
    In demo mode it holds `DemoFixtures`' scripted clients and builds no container. In live mode it builds a container:
    `AddSingleton(configuration)`, `AddProviderConfig().AddOpenAI().AddAnthropic().AddJev().AddChatClient(chatProvider, chatModel)`,
    and `AddGizzardJev(decisionProvider, decisionModel)` when `Gizzard:Decisions` is set. It resolves `Chat` and `Decisions`
    eagerly, so configuration errors surface from `Create`. `Decisions` falls back to `new ChatDecisionClient(Chat)`. `Dispose`
    disposes the container.
  - Configuration keys: `Gizzard:Chat:Provider`, `Gizzard:Chat:Model`, `Gizzard:Decisions:Provider`, `Gizzard:Decisions:Model`.
  - Errors (`InvalidOperationException`, exact text):
    - Live mode without a chat model: `"No chat model configured. Set Gizzard:Chat:Provider and Gizzard:Chat:Model, and declare that provider under Providers."`
    - Only one of the two `Decisions` keys set: `"Gizzard:Decisions needs both Provider and Model."`

- [ ] **Step 1: Write the failing tests** in `ModelServicesTests`, with in-memory configuration:
  - `Demo_mode_needs_no_configuration`: `Chat` and `Decisions` are not null.
  - `Live_without_a_chat_model_throws_naming_the_keys`: the exact message above.
  - `Live_with_an_openai_compatible_provider_returns_a_chat_client`: config `Providers:ollama:Type = openai`,
    `Providers:ollama:Endpoint = http://localhost:11434/v1`, `Gizzard:Chat:Provider = ollama`, `Gizzard:Chat:Model = llama3`.
    `Chat` is not null, and `Decisions` is a `ChatDecisionClient`.
  - `Live_with_jev_decisions_returns_a_jev_decision_client`: plus `Providers:jev:Type = jev`, `Providers:jev:ApiKey = k`,
    `Gizzard:Decisions:Provider = jev`, `Gizzard:Decisions:Model = jev-1.14`. `Decisions` is a `JevDecisionClient`.
  - `Live_with_half_a_decisions_entry_throws`: the exact message above.
  - `Live_with_an_invalid_provider_reports_the_validation_failure`: `Providers:openai:Type = openai` with no key. The thrown
    message contains `"Providers:openai:ApiKey is required for type 'openai' without an Endpoint."`.
  - `Removed_model_flags_are_rejected` (in `CliAppTests`, following that file's style): `run --openai-key x` exits non-zero
    with an unknown-option error.

- [ ] **Step 2: Run them.** Run: `dotnet test --solution PinkRooster.Gizzard.slnx`. Expected: build failure.

- [ ] **Step 3: Implement** `SampleConfiguration`, `ModelServices`, the settings changes and the commands. Each command uses
  `using ModelServices models = ModelServices.Create(SampleConfiguration.Load(), settings.Demo, settings.FailGate)` (`Decompose`
  passes `failGate: false`). It keeps the existing "Model configuration error" panel for any exception from `Create`, and
  `models` stays alive for the whole command.

- [ ] **Step 4: Update the docs** found by the grep. Document the `Providers` and `Gizzard` sections with a filled-in example
  (OpenAI, Ollama and Jev), and setting keys with `dotnet user-secrets set "Providers:openai:ApiKey" "..." --project samples/PinkRooster.Gizzard.SampleCli`.
  Note that `--demo` needs no configuration.

- [ ] **Step 5: Verify.** Run: `dotnet build PinkRooster.Gizzard.slnx -c Release && dotnet test --solution PinkRooster.Gizzard.slnx -c Release`.
  Expected: 0 warnings and all tests pass. Then `dotnet run --project samples/PinkRooster.Gizzard.SampleCli -- demo` should
  complete. `grep -rn "OPENAI_API_KEY\|TYPESAFE_API_KEY\|--openai-key\|--jev-key\|--ollama" samples` should find nothing.

- [ ] **Step 6: Commit.** `refactor(sample-cli)!: configure models through PinkRooster.ProviderConfig`
