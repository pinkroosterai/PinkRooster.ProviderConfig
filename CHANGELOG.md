# Changelog

All notable changes to this project are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [0.1.0-preview.1] - 2026-10-24

Initial release, extracted from the provider layer of PinkRooster.PeckOrder.

### Added

- **`PinkRooster.ProviderConfig`**: `AddProviderConfig()` and its `ProviderConfigBuilder` (`Configure`, `AddProviderType<TClient>`, `AddChatClient`), `IChatClientFactory`, `ProviderOptions`, startup validation of the `Providers` section, and the test-only `ProviderHttpHandler`.
- **`PinkRooster.ProviderConfig.OpenAI`**: provider types `openai` and `openai_responses` (`store: false` unless `Store` is set), `AddOpenAI()`.
- **`PinkRooster.ProviderConfig.Anthropic`**: provider type `anthropic`, `AddAnthropic()`.
- **`PinkRooster.ProviderConfig.Jev`**: provider type `jev` for Jev System One, `AddJev()`, `AddSystemOneClient(...)` and `ISystemOneClientFactory`. The returned `ITypeSafeClient` has the model pinned.
- A warning, logged once per provider, when an API key is sent over plain HTTP to a non-loopback host.

### Changed

- Compared with PeckOrder's providers: the section is the top-level `Providers`, not `PeckOrder:Providers`, and validation messages start with `Providers:{id}`.
