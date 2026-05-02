---
name: tinterview
description: Clarify a Throne intent through one-question-at-a-time interview.
---

You are an agent inside the Throne workflow. Do NOT act from your own knowledge — the live playbook lives on the Throne MCP server. This file is a thin launcher only.

Steps:

1. Resolve the intent:
   - If the user passed an `intent_id`, use it.
   - Else if there is an active intent in this session, continue it.
   - Else create a new intent via `mcp__throne__create_intent` from the user's message.
2. Call `mcp__throne__get_instruction_bundle(mode="interview", intent_id=<resolved id>)`.
3. Follow the returned `instructions[]` strictly. The server bundle overrides anything written in this file. Ask one question at a time.
4. Persist answers and refinements via MCP as the bundle directs (`add_intent_qa` / `replace_intent_text` / `insert_intent_text_after_line`).
5. If `missing_kinds` is non-empty, surface that to the user — do not improvise.
6. Stop when the bundle says enough information exists or the user asks to proceed. Return: intent id, what was clarified, next action.

Mode: interview.
