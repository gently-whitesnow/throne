---
name: dream
description: Thin entry point to the canonical Throne dream skill at skills/dream/SKILL.md. Read dream sources and recent dream sessions, and propose user prompt-part patches via skills/dream/bin/throne-dream. For manual dev sessions opened on the Throne mono-repo (not spawned by the Throne runtime).
---

# Throne Dream (wrapper)

Thin wrapper, not a copy. The skill body is canonical in `skills/dream/SKILL.md`; the CLI is
`skills/dream/bin/throne-dream`. Read the canon for the commands and workflow — they are not
duplicated here so the two never drift.

Just run the commands the canon describes — do not inspect the environment first. Dream is not
intent-scoped; it needs only `THRONE_API_BASE`, which defaults to the local backend
`http://localhost:5008`. If your local Throne API is not running the calls fail with a connection
error — point `THRONE_API_BASE` at a running Throne and retry. See `skills/dream/SKILL.md` for the
full picture.
