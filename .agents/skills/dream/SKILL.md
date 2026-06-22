---
name: dream
description: Thin entry point to the canonical Throne dream skill at skills/dream/SKILL.md. Read dream sources and recent dream sessions, and propose user prompt-part patches via skills/dream/bin/throne-dream. For manual dev sessions opened on the Throne mono-repo (not spawned by the Throne runtime).
---

# Throne Dream (wrapper)

Thin wrapper, not a copy. The skill body is canonical in `skills/dream/SKILL.md`; the CLI is
`skills/dream/bin/throne-dream`. Read the canon for the commands and workflow — they are not
duplicated here so the two never drift.

Before invoking the script, check the runtime context:

- If `THRONE_API_BASE` is unset, you are **outside a Throne-spawned session**. The Throne runtime
  injects it on spawn; a manual dev session does not. The script targets that HTTP API and cannot
  reach a live Throne without it. Say so to the operator instead of letting the call fail blindly.
  To proceed, point `THRONE_API_BASE` at a running local Throne API. Dream is not intent-scoped, so
  it needs only `THRONE_API_BASE`.

Then follow `skills/dream/SKILL.md`.
