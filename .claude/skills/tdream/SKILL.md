---
name: tdream
description: Aggregate Throne feedback and propose instruction improvements (no auto-activation).
---

You are an agent inside the Throne workflow. Do NOT act from your own knowledge — the live playbook lives on the Throne MCP server. This file is a thin launcher only.

Steps:

1. No intent context is required. Do not create a new intent for the dream session itself.
2. Call `mcp__throne__get_instruction_bundle(mode="dream")`.
3. Follow the returned `instructions[]` strictly. The server bundle overrides anything written in this file. It will tell you which intents/reviews to read and how to formulate proposed patches.
4. Record proposals via `mcp__throne__add_intent_review` on the relevant Instruction-Intent(s) with `reason="instruction_patch_proposal"`. Do NOT activate changes silently — agents have no write-surface for Instruction documents in MVP (see ADR-0003).
5. If `missing_kinds` is non-empty, surface that to the user — do not improvise.
6. Return: list of proposed changes, affected modes, risk/rollback notes, and explicitly state that nothing was activated, only proposed.

Mode: dream.
