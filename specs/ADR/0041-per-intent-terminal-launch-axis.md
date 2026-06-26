# ADR-0041: Per-Intent Terminal Launch Axis Persistence

## Status

Accepted

## Context

The intent-page «Запустить агента» panel exposes four launch controls — mode, vendor, model,
effort. Their state lived entirely on the front: `vendor/model/effort` in `useLaunchAxis`'s
`useState`, `mode` in `AgentTerminalPanel`. The panel did not remount per intent and persisted
nothing, so two problems followed:

1. The selection leaked across intents. Switching from intent A (where the operator picked agent
   X) to intent B showed X on B — risking a launch with the wrong agent/model.
2. While a session was live the dropdowns were frozen (`disabled={sessionLive}`) but displayed the
   local draft, not the parameters the session had actually started with. There was no way to see
   what agent/model/effort the running session used.

ADR-0026 §4 deliberately kept Throne stateless about the session: liveness is `tmux has-session`,
and the `Intent` aggregate gains no session fields (`last_session_state`, etc.). That invariant was
about *liveness* and about not bloating the aggregate. It did not anticipate the launch-axis UX
above, which needs a durable, per-intent record that survives reload, another device, and another
UI instance — something front-only `localStorage` cannot provide and which also would not solve
problem 2 (showing the live session's real axis).

## Decision

The backend is the single source of truth for the launch axis. On every successful spawn
(`run`/`restart`) the resolved axis (`mode` + the defaulted `vendor`/`model`/`effort` from
`TerminalLaunchResolver`) is persisted per intent in a dedicated Mongo collection
`terminal_launches` (`_id = intent_id`, one document per intent). It is auxiliary UI-prefill state,
not the session's atomic invariant, so the write is best-effort and runs outside the spawn's
unit-of-work.

The same record carries two skill-selection fields that share its lifecycle, so the launch
modal has one source of truth for «what the operator wants to run next»:

- `attached_skill_ids` — runtime indicator of skills hot-attached into the live tmux session
  (drives the live-session badges).
- `selected_skill_ids_by_mode` — per-mode skill selection persisted on each successful spawn
  and merged on hot-attach for the live session's mode. The next preflight in that mode
  pre-fills with the same set, so a hot-attached skill survives a respawn as default-on
  without a parallel store. Other modes' entries are preserved across writes.

This replaces the earlier `skill_mode_selections` collection — splitting «remembered skills»
across two stores meant hot-attach was invisible to the preview composer when a remembered
set already existed, and the operator's runtime intent was lost on respawn.

Because there is at most one tmux session per intent, the single persisted record serves both
roles: while a session is live it *is* that session's real axis; with no live session it is the
intent's last-used choice. `RunIntentTerminalResponse` and the status probe carry it back in a
nullable `launch` object (null only when the intent was never launched).

The front stops holding «its own truth» for the frozen state:

- Live session → the controls mirror the response's `launch` (read-only, as before).
- No live session → the axis pre-fills from the persisted last-used, falling back to the catalog
  defaults / `default_vendor`.
- The panel remounts per intent (`key={intentId}`) so a draft never leaks between intents.

The lock-while-live behaviour is unchanged: parameters change only through a new run/restart.

## Consequences

### Positive

- Returning to an intent restores its own last choice; no cross-intent leakage.
- A live session's real mode/vendor/model/effort is visible after reload or on another device.
- Liveness stays `tmux has-session`-derived; the `Intent` aggregate is untouched (ADR-0026 §4 holds
  for liveness and for the aggregate — this record lives in its own collection).

### Negative / Risks

- Throne now persists a small amount of per-intent terminal state, narrowing ADR-0026's «persists
  nothing about the session» to «persists no session *state*, but does persist the launch axis».
- The record is not cleaned up on intent `done` (the workspace sweep ignores it); a stale row is
  harmless and overwritten on the next launch.
- Per-mode skill selection lives as a map alongside a single-valued mode/vendor/model/effort.
  The asymmetry is intentional — the launch axis itself is one value (the last spawn's mode), but
  skill defaults are mode-specific by design, so the same intent can carry different remembered
  sets for work vs review and switching mode in the modal flips to the right one.

### Deferred

- A global per-user last-used (the choice is per-intent).
- Editing a live session's parameters without a restart (lock-while-live retained).

## Amendment — ADR-0047 SQLite persistence (2026-06-26)

[ADR-0047](0047-sqlite-ef-core-persistence.md) supersedes the storage details for
`terminal_launches`: launches are stored in the EF Core `terminal_launches` table.
Open extension payloads (`vendor_model`, `allowed_tools`) remain JSON columns using the
shared EF JSON policy.
