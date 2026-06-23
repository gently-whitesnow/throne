---
name: intent
description: Thin entry point to the canonical Throne intent skill at skills/intent/SKILL.md. Read, edit, and decompose the current Intent.text via skills/intent/bin/throne-intent. For manual dev sessions opened on the Throne mono-repo (not spawned by the Throne runtime).
---

# Throne Intent (wrapper)

Thin wrapper, not a copy. The skill body is canonical in `skills/intent/SKILL.md`; the CLI is
`skills/intent/bin/throne-intent`. Read the canon for the commands, concurrency rules, and
constraints — they are not duplicated here so the two never drift.

Pick the command for the task — do not inspect the environment first. To create an intent use
`create` (no `THRONE_INTENT_ID` needed — it works in a bare shell); to read or refine the current
intent use `get` / `replace-text` / `link`. The script degrades gracefully: `THRONE_API_BASE`
defaults to the local backend `http://localhost:5008`, and an unset `THRONE_INTENT_ID` simply means
there is no current intent (so `create`, not `get`). See `skills/intent/SKILL.md` for the full
picture.
