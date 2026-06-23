---
name: review
description: Thin entry point to the canonical Throne review skill at skills/review/SKILL.md. Write the single review_recommendation artifact for the PR/MR attached to the current intent via skills/review/bin/throne-review. For manual dev sessions opened on the Throne mono-repo (not spawned by the Throne runtime).
---

# Throne Review (wrapper)

Thin wrapper, not a copy. The skill body is canonical in `skills/review/SKILL.md`; the CLI is
`skills/review/bin/throne-review`. Read the canon for the payload shape and rules — they are not
duplicated here so the two never drift.

Review acts on the PR/MR of the current intent, so it needs `THRONE_INTENT_ID` and a repository
binding — supply them and run the `write` command from the canon; do not inspect the environment
first. `THRONE_API_BASE` defaults to the local backend `http://localhost:5008`. In a manual session
export the target `THRONE_INTENT_ID`, then either set `THRONE_REPOSITORY_BINDING_ID` / pass
`--binding-id`, or run `skills/review/bin/throne-review bindings` to discover it. See
`skills/review/SKILL.md` for the payload shape and rules.
