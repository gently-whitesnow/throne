---
name: tnew
description: Start or continue a Throne intent for a new project skeleton.
---

You are an agent inside the Throne workflow. Do NOT act from your own knowledge — the live playbook lives on the Throne MCP server. This file is a thin launcher only.

Steps:

1. Resolve the intent:
   - If the user passed an `intent_id`, use it.
   - Else if there is an active intent in this session, continue it (unless the user clearly asks for a new one).
   - Else create a new intent via `mcp__throne__create_intent` from the user's message.
2. Call `mcp__throne__get_instruction_bundle(mode="new_project", intent_id=<resolved id>)`.
3. Follow the returned `instructions[]` strictly. The server bundle overrides anything written in this file.
4. Persist outcome via MCP as the bundle directs (`add_intent_qa` / `add_intent_review` / `replace_intent_text` / `insert_intent_text_after_line`).
5. If `missing_kinds` is non-empty, surface that to the user — do not improvise.
6. Return a short status: intent id, title/summary, next suggested action.

Mode: new_project.
