# ADR-0043: Static Operational Skills And MCP Removal

Status: Accepted

Date: 2026-06-21

Related: [ADR-0003](0003-mcp-text-editing-semantics.md), [ADR-0004](0004-mcp-call-audit-log.md), [ADR-0013](0013-mcp-attachment-delivery-tools.md), [ADR-0014](0014-mcp-initialize-instructions-routing.md), [ADR-0022](0022-frontier-driven-dream-flow.md), [ADR-0023](0023-mcp-tools-snake-case-naming.md), [ADR-0030](0030-mcp-surface-policy-cli-first.md), [ADR-0034](0034-dual-execution-contours-hooks-vs-bundles.md), [ADR-0037](0037-direct-http-mcp-for-standalone-agents.md)

## Context

Operational agent support was split across two delivery mechanisms:

- generated per-session `SKILL.md` files for intent/review operations;
- a Throne MCP server with tools for intent, dream, prompt-part patching, attachments, and repository reads.

This made operations harder to review and reuse: some behavior lived in C# string generation, some in MCP tool code, and some in runtime prompts. It also kept a standalone MCP contour alive even though the product path is the embedded terminal.

The system-prompt layer is separate. `specs/manifest/throne-skills.yaml`, Mongo user prompt parts, and `PromptCompositionResolver` still compose mode instructions. This ADR changes only the operational layer.

## Decision

Throne has three provider-neutral operational skills as static repo files:

- `skills/intent/SKILL.md`
- `skills/review/SKILL.md`
- `skills/dream/SKILL.md`

Each skill calls a static CLI script from `bin/`:

- `skills/intent/bin/throne-intent` edits `Intent.text`, creates child intents, and links intents.
- `skills/review/bin/throne-review` writes the single `review_recommendation` repository artifact.
- `skills/dream/bin/throne-dream` reads dream sources/sessions, reads current prompt parts, proposes prompt-part patches, and records dream sessions.

The scripts use the public HTTP API on `THRONE_API_BASE` and ambient session context:

- `THRONE_INTENT_ID` for intent-scoped operations;
- `THRONE_REPOSITORY_BINDING_ID` when review has a selected binding.

Session spawn injects those variables into tmux through `tmux new-session -e`. The skill bodies stay static: no intent id, paths, API URLs, or binding ids are substituted into `SKILL.md`.

The session skill catalog exposes exactly `intent`, `review`, and `dream`. Default mode mapping remains:

- `interview` -> `intent`
- `review` -> `review`
- `dream` -> `dream`

The operator can still add or remove any of the three skills on any run; mode continues to affect only system-prompt composition.

Vendor adapters materialize the same source skills into a workspace-local canonical location
and vendor-specific discovery files:

- Canonical source for the session: `skills/<id>/SKILL.md`
- Claude pointer: `.claude/skills/<id>/SKILL.md`
- Codex pointer: `.agents/skills/<id>/SKILL.md`
- OpenCode: `throne-session.<id>.md` instruction files

## MCP Retirement

The Throne MCP server, all MCP tools, MCP SDK packages, MCP audit wrapper, and `mcp_call_log` write/index path are removed.

ADR-0004 audit requirements retire with the MCP server. CLI calls are not reimplemented as an audit log; writes still enter the HTTP API and emit the existing domain/realtime events where applicable.

ADR-0037 standalone direct HTTP MCP is retired. There is no `/mcp` endpoint and no external MCP contour. The supported dogfooding case is a normal agent session opened in the Throne repo with the same static skills attached and `THRONE_API_BASE` pointing at a running local API.

## Consequences

- Operational behavior is versioned and reviewed as repo files.
- Providers share one source of operational instructions instead of separate generated skill bodies.
- `PromptPartPatch` UI and contracts remain; dream now reaches them through `skills/dream/bin/throne-dream`.
- `dream_sources` remain manifest data.
- Intent status is not agent-written. Embedded session hooks derive status transitions.
- Layer A system-prompt composition remains unchanged.

## Rejected

- **Session context file / walk-up discovery.** Rejected for now. Spawn-time environment variables are enough for embedded sessions and keep the skill files static.
- **Migrating MCP audit to CLI audit.** Rejected because the audit existed to guard MCP tool registration and invocation, both of which are removed.
- **Keeping standalone MCP for external agents.** Rejected to avoid maintaining a second operational contour.
