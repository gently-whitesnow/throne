---
name: intent
description: Thin entry point to the canonical Throne intent skill at skills/intent/SKILL.md. Read, edit, and decompose the current Intent.text via skills/intent/bin/throne-intent. For manual dev sessions opened on the Throne mono-repo (not spawned by the Throne runtime).
---

# Throne Intent (wrapper)

Thin wrapper, not a copy. The skill body is canonical in `skills/intent/SKILL.md`; the CLI is
`skills/intent/bin/throne-intent`. Read the canon for the commands, concurrency rules, and
constraints — they are not duplicated here so the two never drift.

Before invoking the script, check the runtime context:

- If `THRONE_INTENT_ID` or `THRONE_API_BASE` is unset, you are **outside a Throne-spawned
  session**. The Throne runtime injects these on spawn; a manual dev session does not. The script
  is intent-scoped and cannot resolve an intent without them. Say so to the operator instead of
  letting the call fail blindly. To proceed, point `THRONE_API_BASE` at a running local Throne API
  and export the target `THRONE_INTENT_ID`.

Then follow `skills/intent/SKILL.md`.
