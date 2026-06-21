---
name: dream
description: Run Throne dream operations through bin/throne-dream: read sources/sessions, propose prompt-part patches, and record dream sessions.
---

# Throne Dream Operations

Use `bin/throne-dream` for dream-mode memory and user prompt-part patch proposals. The script reads `THRONE_API_BASE`.

Commands:

```bash
bin/throne-dream sources
bin/throne-dream sessions --host "$(hostname)" --limit 5
bin/throne-dream current-part --scope user --key work
bin/throne-dream patches --scope user --key work --limit 50
bin/throne-dream propose-patch < /tmp/prompt-part-patch.json
bin/throne-dream record-session < /tmp/dream-session.json
```

Workflow:

1. Read dream sources, then inspect local conversations from those paths.
2. List recent dream sessions for the current host before selecting the next conversation frontier.
3. Read current user prompt parts before proposing patches. Use the returned `current_version` as `base_version`.
4. Propose PromptPartPatch records for user parts only. The operator applies, edits, or rejects them in the UI.
5. Record a DreamSession with processed conversation ids, summary, reflection, and proposed patch ids.

Do not patch system prompt parts. Do not write user parts directly; propose patches and leave final decisions to the operator.
