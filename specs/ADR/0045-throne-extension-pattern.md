# ADR-0045: Throne Extension Pattern

## Status

Accepted

Date: 2026-06-22

Related: [ADR-0024](0024-intent-repository-binding-and-cli-providers.md),
[ADR-0026](0026-embedded-terminal-capabilities-and-run-preflight.md),
[ADR-0032](0032-gitlab-provider.md),
[ADR-0043](0043-static-operational-skills-and-mcp-removal.md)

## Context

Throne grows along a handful of pluggable axes — git providers (`github`, `gitlab`), agent vendors
(`claude`, `codex`, `opencode`), carrier capabilities (`open_in_ide`), operational skills
(`intent`, `review`, `dream`). Each is in-tree and local-first (no dynamic plugin host,
[ADR-0043](0043-static-operational-skills-and-mcp-removal.md)); a new variant is a code change, not
a runtime install.

The same seam recurs at every axis: a port in `Throne.Application`, one implementation per variant
in `Throne.Infrastructure` (or, for pure metadata, a descriptor record in Application), a DI fan-out
that registers all of them as `IEnumerable<T>`, and a registry that indexes them by a string key for
O(1) lookup. **Git providers** and the **capability probes** already did this cleanly. Three other
axes had drifted off their own baseline: agent vendors lived in a sealed static catalog with a
hand-maintained `Descriptors` list + `ByVendor` dictionary and vendor-specific `if (vendor == …)`
branches in spawn / hot-attach; operational skills had a static catalog plus a hardcoded
mode→skill seed and a `switch` over skill ids to build packages; the git-status endpoint hardcoded
`GetByName("github")` / `GetByName("gitlab")` instead of iterating the registry.

The cost of that drift is paid every time the Nth variant is added: edits scattered across a static
list, a lookup dictionary, a seed table, and one or more `switch`/`if` sites — easy to miss one, and
no single place that says "this is how you add a provider".

## Decision

Adopt one canonical idiom for every in-tree extensibility axis, with **Capabilities**
(`ICapabilityProbe` + `CapabilityCatalog` + the DI detection cache) as the maturity reference:

```
port (Application interface or descriptor record)
  → one implementation/descriptor per variant
  → DI fan-out: AddSingleton<TPort, TImpl>() (one line per variant)
  → registry indexes IEnumerable<TPort> by a string key (ordinal), preserving registration order
  → callers resolve via the registry; no static aggregate list, no switch on the key
```

Per-variant *behaviour differences* travel as data on the descriptor (flags, factory `Func`s), not
as `if (key == …)` branches in the execution path. Examples codified by this ADR:

- **Agent vendors.** Descriptors moved out of the static catalog into individual DI registrations
  behind `ITerminalVendorCatalog` (mirrors `IGitProviderRegistry`). Vendor-specific execution
  branches became descriptor flags: `EnableMouse` (OpenCode pane) and `SupportsNativeHotAttach`
  (Claude/Codex). `TerminalAgentCatalog` keeps only stable wire tokens (vendor names, the closed
  effort set, model-source constants).
- **Operational skills.** `SessionSkillCatalog` indexes DI-registered `SessionSkillDescriptor`s.
  The mode→skill default seed is `DefaultModes` on the descriptor; package construction (including
  materialisability) is a `CreatePackage` factory on the descriptor — folding away the seed table
  and the package-building `switch`.
- **Git providers.** The status endpoint iterates `IGitProviderRegistry.AllProviders` to probe
  every registered provider; no per-provider `GetByName`.

### Checklist — adding the Nth variant

1. **Provider/vendor (behaviour).** Add the implementation class in `Throne.Infrastructure`
   implementing the Application port (`IGitProvider`, `ICapabilityProbe`, `IIdeOpener`,
   `IAgentVendorLoginProbe`, `IVendorModelCatalog`). Register one `AddSingleton<TPort, TImpl>()`
   line. The registry picks it up — no list/switch edit.
2. **Vendor (metadata).** Add a `TerminalVendorDescriptor` to `TerminalVendorDescriptors` and one
   `AddSingleton(…)` line in the Application composition root. Express any per-vendor execution
   difference as a descriptor flag, not a new `if`.
3. **Skill.** Drop the canon under `skills/<id>/` (`SKILL.md` + `bin/throne-*`,
   [ADR-0043](0043-static-operational-skills-and-mcp-removal.md)); add a `SessionSkillDescriptor`
   (with `DefaultModes` + `CreatePackage`) to `SessionSkillDescriptors` and one `AddSingleton(…)`
   line. No edits to seeds or the package registry.
4. **Wire DTO.** If the variant must appear on a closed OpenAPI object with one field per variant
   (e.g. `GitProvidersStatusDto.github`/`.gitlab`), add the field + its codegen — see "known tax".

## Consequences

### Positive

- Adding a variant is mechanical and single-sited: a new class/descriptor + one DI line. No
  central aggregate list or `switch` to find and edit.
- Behaviour stays on the descriptor, so the execution path is variant-agnostic — fewer places to
  miss when a new variant has a slightly different shape.
- The four axes now share one mental model and one reference (Capabilities), making the codebase
  easier to learn and review.

### Negative / Risks / Known tax

- **Wire-mapper tax.** Where a variant set is exposed as a *closed* OpenAPI object with one named
  field per variant (`GitProvidersStatusDto`), the projection onto those fields stays a per-field
  mapper even though the probing loop is generic. Opening that wire shape (an enum→open string, a
  map-typed payload) touches codegen ([ADR-0006](0006-openapi-contract-first-codegen.md)) and
  frontend type-safety and is **out of scope** here — accepted as a known tax until a dedicated
  decision.
- **Not applied to strategy-dispatch switches.** Some `switch (vendor)` sites are genuine strategy
  dispatch over divergent side-effects (e.g. `SessionSkillMaterializer` writing vendor-specific
  skill file layouts), not capability flags. Those are left as-is; this ADR governs the
  registration/lookup seam and capability-flag branches, not every per-variant code path.
- DI now owns aggregation: a missing `AddSingleton` line silently drops a variant. The registries
  throw on duplicate keys, and arch/contract tests cover the wired surface, but registration is the
  single point that must not be forgotten.
