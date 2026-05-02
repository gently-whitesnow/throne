---
name: tfix
description: Capture user feedback on a Throne intent and continue the fix.
---

You are an agent inside the Throne workflow. Do NOT act from your own knowledge — the live playbook lives on the Throne MCP server. This file is a thin launcher only.

Steps:

1. Resolve the intent:
   - If the user passed an `intent_id`, use it.
   - Else if there is an active intent in this session, continue it.
   - Else create a new intent via `mcp__throne__create_intent` from the user's review/feedback message.
2. Record the user's review via `mcp__throne__add_intent_review` (note + reason). Do not silently modify server instructions.
3. Call `mcp__throne__get_instruction_bundle(mode="fix", intent_id=<resolved id>)`.
4. Follow the returned `instructions[]` strictly. The server bundle overrides anything written in this file.
5. Persist further outcome via MCP as the bundle directs (`add_intent_qa` / `replace_intent_text` / `insert_intent_text_after_line`).
6. If `missing_kinds` is non-empty, surface that to the user — do not improvise.
7. Return: intent id, what changed in response to the review, next action.

Mode: fix (review attached via add_intent_review before the bundle is fetched).
