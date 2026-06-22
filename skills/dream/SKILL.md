---
name: dream
description: Available for reading Throne dream sources and recent dream sessions to pull conversation context for the current host, and for proposing user prompt-part patches plus recording dream sessions through skills/dream/bin/throne-dream.
---

# Throne Dream Operations

Use `skills/dream/bin/throne-dream` for dream-mode memory and user prompt-part patch proposals. The script reads `THRONE_API_BASE`.

Commands:

```bash
skills/dream/bin/throne-dream sources
skills/dream/bin/throne-dream sessions --host "$(hostname)" --limit 5
skills/dream/bin/throne-dream current-part --scope user --key work
skills/dream/bin/throne-dream patches --scope user --key work --limit 50
skills/dream/bin/throne-dream propose-patch < /tmp/prompt-part-patch.json
skills/dream/bin/throne-dream record-session < /tmp/dream-session.json
```

Workflow:

1. Read dream sources, then inspect local conversations from those paths.
2. List recent dream sessions for the current host before selecting the next conversation frontier.
3. Read current user prompt parts before proposing patches. Use the returned `current_version` as `base_version`.
4. Propose PromptPartPatch records for user parts only. The operator applies, edits, or rejects them in the UI.
5. Record a DreamSession with processed conversation ids, summary, reflection, and proposed patch ids.

Do not patch system prompt parts. Do not write user parts directly; propose patches and leave final decisions to the operator.
