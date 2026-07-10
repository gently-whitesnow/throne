---
name: intent
description: Available for reading the current Throne Intent text, editing it, and decomposing it into child intents (create + link) through skills/intent/bin/throne-intent. Routine operation whenever the session needs to inspect or refine the intent.
---

# Throne Intent Operations

Use `skills/intent/bin/throne-intent` to read the current Intent text, edit it, or decompose it into
child intents. The skill ships in this repo; run it from the workspace root with the relative path
shown below.

## Pick the command for the task

Choose by what you were asked to do — do not inspect the environment first.

- **Create an intent** — asked to create one, or there is no current intent → `create`. It needs no
  `THRONE_INTENT_ID` and works in a bare/standalone shell.
- **Refine the current intent body** → `replace-text` (it re-reads the intent itself for concurrency).
- **Decompose the current intent** → `create` + `link`.

You usually already have the current `Intent.text` in your session context — do not call `get` just
to read it. Reach for `get` only when you genuinely lack the text and need to fetch it.

Anti-pattern: do not run `get` as a connectivity smoke test when there is no current intent — it
will correctly fail with `THRONE_INTENT_ID is required`. If the task is to create an intent, just
call `create`.

## Commands

```bash
skills/intent/bin/throne-intent get
skills/intent/bin/throne-intent replace-text --old-file /tmp/throne-old.txt --new-file /tmp/throne-new.txt
child_id="$(skills/intent/bin/throne-intent create --text-file /tmp/throne-child.md --id-only)"
skills/intent/bin/throne-intent link "$child_id" --blocking false --rationale-file /tmp/throne-rationale.txt
```

`replace-text` handles optimistic concurrency by reading the current intent immediately before
writing; the old fragment must occur exactly once in current `Intent.text`.

## Environment

The script reads two variables from the environment. A Throne-spawned session has them set; a
manual/standalone session may not — and that is fine, the script degrades gracefully.

- `THRONE_API_BASE` — optional. Defaults to the local backend `http://localhost:5008`.
- `THRONE_INTENT_ID` — optional. Needed only for `get`, `replace-text`, and `link`.
  Empty means there is no current intent → use `create`.

## Rules

- Do not write intent status from the agent. Throne derives status from session hooks.
- Use `--blocking true` only for a hard dependency edge; otherwise keep it `false`.
- An intent tag is **not** a free-form label. It carries exactly one meaning: the bare repository
  name in its native form (e.g. `throne`). This is identical for intents created from scratch and
  for child intents.
  - Do not use the workspace slug (`gently-whitesnow__throne`) — only the clean repo name (`throne`).
  - Do not invent arbitrary, thematic, or feature tags.
  - You determine the repository name yourself from the working context.
- Pass `--tag <repo-name>` on `create` to set this tag; omit it when the repository is already
  unambiguous from the parent. Never pass more than the repository-name tag.
