---
name: review
description: Thin entry point to the canonical Throne review skill at skills/review/SKILL.md. Write the single review_recommendation artifact for the PR/MR attached to the current intent via skills/review/bin/throne-review. For manual dev sessions opened on the Throne mono-repo (not spawned by the Throne runtime).
---

# Throne Review (wrapper)

Thin wrapper, not a copy. The skill body is canonical in `skills/review/SKILL.md`; the CLI is
`skills/review/bin/throne-review`. Read the canon for the payload shape and rules — they are not
duplicated here so the two never drift.

Before invoking the script, check the runtime context:

- If `THRONE_INTENT_ID` or `THRONE_API_BASE` is unset, you are **outside a Throne-spawned
  session**. The Throne runtime injects these on spawn; a manual dev session does not. Without them
  the script cannot resolve the intent or its repository binding. Say so to the operator instead of
  letting the call fail blindly. To proceed, point `THRONE_API_BASE` at a running local Throne API,
  export the target `THRONE_INTENT_ID`, and supply `--binding-id` (or `THRONE_REPOSITORY_BINDING_ID`).

Then follow `skills/review/SKILL.md`.
