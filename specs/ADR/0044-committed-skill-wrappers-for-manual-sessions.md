# ADR-0044: Committed Skill Wrappers For Manual Sessions

## Status

Accepted

Date: 2026-06-22

Related: [ADR-0043](0043-static-operational-skills-and-mcp-removal.md)

## Context

[ADR-0043](0043-static-operational-skills-and-mcp-removal.md) made `intent`, `review`, and `dream`
static repo files under `skills/<id>/SKILL.md` (+ `skills/<id>/bin/throne-*`). The Throne runtime
hands them to agents only on spawn: the session skill catalog materializes each `SKILL.md` into a
vendor-scanned location (Claude — `.claude/skills/<id>/`; Codex — inline hints; OpenCode —
instruction files), and `WorkspaceStagingReset` clears those projections before each preflight.

That path runs only inside Throne-spawned sessions. A developer who opens the Throne mono-repo in a
plain agent session (no Throne spawn) gets no commands: Claude Code scans `.claude/skills/`,
`~/.claude/skills/`, and plugins — never the root `skills/`; Codex scans `.agents/skills/<name>/`
(and user-level `~/.codex` / `~/.agents`) — also not the root `skills/`. So `/dream`, `/intent`,
`/review` are absent until a skill is hand-copied into a scanned directory.

## Decision

Commit **thin wrapper** `SKILL.md` files into the directories the tools actually scan:

- Claude: `.claude/skills/<name>/SKILL.md`
- Codex: `.agents/skills/<name>/SKILL.md`

A wrapper does **not** copy the skill body. It carries frontmatter (`name`/`description`) so the
tool registers the command, then points at the canon `skills/<name>/SKILL.md` and its
`skills/<name>/bin/throne-*` script. The single source of truth stays in `skills/`; wrappers are
entry points only. Because the `SKILL.md` format is shared between Claude and Codex, the two
wrappers for a given skill are byte-identical and differ only by directory.

Each wrapper opens with a runtime-context check so it **degrades legibly**: outside a Throne spawn
there is no `THRONE_API_BASE` / `THRONE_INTENT_ID` (and, in arbitrary clones, possibly no
`bin/throne-*`). The wrapper tells the agent it is outside the Throne runtime and what to set,
rather than letting the underlying script fail silently. In the mono-repo the `bin/throne-*`
scripts are present, but env is still unset — the wrapper says so.

## Coexistence With The Runtime

Committed wrappers serve only non-spawn sessions. Throne-spawned sessions are unaffected: the
catalog materializer and `WorkspaceStagingReset` operate exactly as before. The wrappers and the
canon never collide in a manual session because the tools scan `.claude/skills/` / `.agents/skills/`
but not the root `skills/`.

One id-collision caveat: `WorkspaceStagingReset` deletes `.claude/skills/{intent,review,dream}` and
`skills/{intent,review,dream}` at the start of every spawn. If the Throne runtime is ever pointed at
the Throne repo itself as a spawn workspace, that reset would wipe both the committed wrappers and
the canon. That spawn-on-self case is out of scope for this slice; it must be handled before
enabling it.

## Maintenance Convention

Wrappers are written by hand for now. When a new operational skill is added under `skills/<name>/`:

1. Add `.claude/skills/<name>/SKILL.md` and `.agents/skills/<name>/SKILL.md`.
2. Keep both byte-identical; never copy the canon body into them — point at
   `skills/<name>/SKILL.md` and `skills/<name>/bin/throne-*`.
3. Include the runtime-context degradation note.

Auto-generating wrappers from `skills/` (so a new skill yields both wrappers without editing three
places) is the intended evolution but is not part of this slice.

## Consequences

- `/dream`, `/intent`, `/review` work out of the box in manual Claude and Codex dev sessions on the
  mono-repo, with no runtime involvement.
- Skill bodies stay single-sourced in `skills/`; wrappers cannot drift because they hold no body.
- Three places per skill must be edited by hand until auto-generation lands.

## Rejected

- **Copying the skill body into each wrapper.** Rejected — duplication drifts; the wrapper stays a
  pointer.
- **Generating wrappers at build/preflight time.** Deferred, not rejected — desirable, but a larger
  slice than committing the three pairs by hand.
- **Provisioning canon + env into arbitrary user repos.** Out of scope — that is the runtime
  spawn-materialization path, a separate, larger slice.
