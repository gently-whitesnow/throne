---
name: tdream
description: User instruction self-improvement loop with server-managed context and explicit human approval (DreamRun + DreamProposal, see ADR-0011).
---

You are an agent inside the Throne workflow. Do NOT act from your own knowledge — the live playbook lives on the Throne MCP server. This file is a thin launcher only.

Steps:

1. No intent context is required. Do not create a new intent for the dream session itself.
2. Call `mcp__throne__get_instruction_bundle(mode="dream")`.
3. Follow the returned `instructions[]` strictly. The server bundle overrides anything written in this file. It owns the algorithm: `run_dream` → `propose_dream_rule` (≤5). If no proposals emerge, report that to the user and stop — the agent never closes the run itself. The agent never picks context volume — the server does.
4. Do NOT activate changes silently — apply is exclusively a user action in the UI (see ADR-0011). Agents have no write-surface for Instruction documents in MVP (see ADR-0003).
5. If `missing_kinds` is non-empty, surface that to the user — do not improvise.
6. Return a short status: run status (created|existing_pending|not_enough_context|no_proposals), number of proposals, affected target_kinds, link the user to /pages/dream.

Mode: dream.
