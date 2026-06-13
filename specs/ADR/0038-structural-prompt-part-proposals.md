# ADR-0038: Structural PromptPart proposals through MCP

## Status

Accepted
Date: 2026-06-13
Amends [ADR-0030](0030-mcp-surface-policy-cli-first.md) and [ADR-0036](0036-unify-prompt-part-entity-and-rename-mcp.md).
Related: [ADR-0034](0034-dual-execution-contours-hooks-vs-bundles.md).

## Context

[ADR-0036](0036-unify-prompt-part-entity-and-rename-mcp.md) kept MCP write access limited to
`propose_prompt_part_patch`, and apply/reject stayed an operator decision in `/improvements`.
That was enough for replacing text of an existing user part, but not enough for maintaining the
prompt-part set itself.

The missing case is decomposition of a monolithic user part into core instructions plus optional
stack-specific parts. A new runtime key cannot be created reliably through the old text-only
proposal path: lazy create derived roles from the manifest, so non-manifest keys received an empty
`mode_roles` set and became unavailable in embedded composition.

Direct agent CRUD would solve the mechanics but would introduce a second trust path beside the
existing proposal -> operator apply funnel.

## Decision

`PromptPartPatch` carries an explicit `operation`:

- `replace_text`: replace the target user part text; this is the ADR-0036 behavior.
- `create`: create a missing user part from `patch_text` and the supplied `mode_roles`.
- `set_roles`: replace the target user part `mode_roles`.
- `delete`: delete the target user part after detaching it from all modes.

The existing MCP tool `propose_prompt_part_patch` is extended with optional fields instead of adding
direct CRUD tools:

- `operation` defaults to `replace_text` for backward-compatible callers.
- `mode_roles[] = {mode, role, order}` is required for `create` and `set_roles`.
- `patch_text` remains the text payload for `replace_text` and `create`; it may be empty for
  structural operations.

Apply/reject remain operator actions through the existing `/improvements` patch endpoints. The
operator UI renders structural payloads directly: role tables for `create` / `set_roles`, text
preview for `create`, and an explicit delete confirmation for `delete`.

Only `scope=user` remains patchable. `scope=system` stays manifest-managed and read-only from the
MCP proposal surface.

## Consequences

Positive:

- Agents can fully maintain user prompt parts without gaining direct apply authority.
- Optional user parts no longer rely on manifest-derived mandatory roles.
- `/improvements` remains the single review and audit funnel.

Negative / risks:

- `PromptPartPatch` is no longer text-only; consumers must branch by `operation`.
- Structural operations do not always increment `PromptPart.current_version`, so `applied_version`
  records the post-apply current version rather than always `base_version + 1`.

Out of scope:

- Agent-side apply/reject.
- System prompt part authoring through MCP.
- Automatic relevance selection of optional parts.
