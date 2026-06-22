# ADR-0046: Open Wire Keys for Extension Axes

## Status

Accepted

Date: 2026-06-22

Related: [ADR-0006](0006-openapi-contract-first-codegen.md),
[ADR-0045](0045-throne-extension-pattern.md),
[ADR-0024](0024-intent-repository-binding-and-cli-providers.md),
[ADR-0041](0041-per-intent-terminal-launch-axis.md)

## Context

ADR-0045 fixed the in-process extension idiom: a pluggable axis is a port or descriptor, one
variant per implementation, DI fan-out, then a registry indexed by a stable string key. Git
providers (`IGitProviderRegistry`) and terminal vendors (`ITerminalVendorCatalog`) already follow
that shape, so adding the next variant should be a class or descriptor plus one DI registration.

The HTTP boundary still closes two of those axes as OpenAPI enums:

- `repositories#/components/schemas/GitProvider` is `github | gitlab`.
- `tags#/components/schemas/TagDefaultGitProvider` mirrors the same enum because NSwag does not
  resolve cross-document refs.
- `terminal#/components/schemas/TerminalAgentVendor` and the settings mirror are
  `claude | codex | opencode`.

Codegen turns those enums into C# enums and TS unions. That gives autocomplete and compile-time
exhaustiveness on the client, but it reintroduces a cross-boundary tax for axes that are intended to
be extension points: a new provider/vendor needs OpenAPI edits, generated DTO churn, frontend union
updates, and exhaustive mapper switches. Missing one mapper turns into a runtime throw.

The repository already uses the other approach for carrier capabilities: a capability name can be a
closed enum, but each capability's `provider` is an open string returned by the backend catalog and
validated server-side.

## Decision

Use **open string wire keys** for both extension axes:

1. `provider` for git repositories and tag default repositories becomes `type: string` with normal
   string constraints, not an OpenAPI enum.
2. `vendor` for terminal launch, terminal catalog, persisted terminal settings, and launch echoes
   becomes `type: string` with normal string constraints, not an OpenAPI enum.
3. Closed OpenAPI enums stay for genuinely closed protocol/state axes: run modes, session states,
   clone statuses, PR states, reasoning-effort tiers, login states, model-source kinds, etc.

The stable key is still owned by the backend variant descriptor:

- Git: `IGitProvider.ProviderName`, indexed by `IGitProviderRegistry`.
- Terminal: `TerminalVendorDescriptor.Vendor`, indexed by `ITerminalVendorCatalog`.

Open string is not "accept anything". Unknown values are rejected at the first server boundary that
can resolve the axis.

## Validation

Inbound git provider values are validated through `IGitProviderRegistry`:

- path parameters such as `/api/v1/git-providers/{provider}/...` resolve with
  `GetByName(provider)`;
- request bodies such as intent bindings and tag default repositories use the same lookup before
  persistence or side effects;
- an unknown value returns the existing validation/problem flow (`422` with a provider-unsupported
  error), not a generated-enum bind failure.

Inbound terminal vendor values are validated through `ITerminalVendorCatalog`:

- launch requests and settings writes call `Find`/`IsKnownVendor` before resolving defaults or
  persisting `default_terminal_vendor`;
- persisted legacy values that no longer have a descriptor fall back to the catalog default on read,
  matching the existing `MongoTerminalSettingsStore` behaviour;
- launch echoes and vendor catalog rows write the descriptor key directly.

Mapper switches whose only job was enum translation are removed. Strategy switches may remain when
they encode real behavioural differences that cannot live on a descriptor.

## Catalog Delivery

The frontend must not discover valid extension keys from generated enum types.

- Terminal already has the canonical catalog: `GET /api/v1/terminal/vendors`. It returns every
  registered vendor, the default vendor, selectability, model lists, effort support, and login
  status. Launch and settings controls use this response as the source of truth.
- Git needs the same catalog-style source. The fixed-field settings response
  `GitProvidersStatusDto.github/gitlab` should be replaced by a collection keyed by provider name,
  or a dedicated `GET /api/v1/git-providers` catalog/status response backed by
  `IGitProviderRegistry.AllProviders`. Frontend repository and settings controls use that response
  instead of hardcoded provider unions.

OpenAPI remains the shape contract (ADR-0006), but no longer carries the value set for these axes.

## Drift Detection

Because codegen no longer fails when a provider/vendor key is missing from a union, drift moves to
targeted tests:

- contract tests assert the extensible schemas are plain strings and do not regain `enum`;
- API/catalog tests assert every registered git provider and terminal vendor appears in the
  corresponding catalog response exactly once;
- validation tests assert an unknown git provider/vendor is rejected with the expected problem code;
- mapper or endpoint tests cover the round trip from registered descriptor key to DTO and from
  inbound string to registry lookup.

The `contracts` gate from ADR-0006 still runs and still detects shape drift and generated-file
drift. It is not responsible for extension-key drift after this ADR.

## Consequences

### Positive

- Adding the Nth git provider or terminal vendor no longer changes OpenAPI just to name the key.
- The wire contract matches the in-process extension model from ADR-0045.
- Frontend controls are driven by backend catalogs, so deploy-time registration is the single source
  of truth for available variants.

### Negative / Risks

- Generated TypeScript loses `provider`/`vendor` literal unions and autocomplete.
- Unknown values are detected at runtime validation instead of by generated client types.
- Existing clients that relied on exhaustive frontend switches must become catalog-driven and handle
  unknown-but-registered keys generically.

### Migration Notes

- Replace `GitProvider`, `TagDefaultGitProvider`, and mirrored `TerminalAgentVendor` schemas with
  constrained strings.
- Regenerate contracts per ADR-0006 and remove enum-translation mappers for these axes.
- Convert fixed git-provider status fields to a map or list shape before removing the last
  hardcoded `github`/`gitlab` projection.
- Keep closed enums for non-extension state machines and protocol values.
