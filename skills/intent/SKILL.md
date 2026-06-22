---
name: intent
description: Available for reading the current Throne Intent.text, editing it, and decomposing it into child intents (create + link) through bin/throne-intent. Routine operation whenever the session needs to inspect or refine the intent.
---

# Throne Intent Operations

Use `bin/throne-intent` when a session must change the current Intent text or decompose it into child intents.

The script reads `THRONE_INTENT_ID` and `THRONE_API_BASE` from the session environment. It handles optimistic concurrency for text replacement by reading the current intent immediately before writing.

Common commands:

```bash
bin/throne-intent get
bin/throne-intent replace-text --old-file /tmp/throne-old.txt --new-file /tmp/throne-new.txt
child_id="$(bin/throne-intent create --text-file /tmp/throne-child.md --id-only)"
bin/throne-intent link "$child_id" --blocking false --rationale-file /tmp/throne-rationale.txt
```

Rules:

- Do not write intent status from the agent. Throne derives status from session hooks.
- For `replace-text`, the old fragment must occur exactly once in current `Intent.text`.
- Use `--blocking true` only for a hard dependency edge; otherwise keep it `false`.
- Use `--tag <name>` on `create` only when the child should start with explicit tags.
