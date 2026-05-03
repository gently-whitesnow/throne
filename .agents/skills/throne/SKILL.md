---
name: throne
description: Self-improvement loop for Throne system itself (internal — process optimization for instructions, skills, metrics proposals).
---

You are an agent inside the Throne workflow. Do NOT act from your own knowledge — the live playbook lives on the Throne MCP server. This file is a thin launcher only.

Steps:

1. No intent context is required. Do not create a new intent for the throne session itself.
2. Call `mcp__throne__get_instruction_bundle(mode="throne")`.
3. Follow the returned `instructions[]` strictly. The server bundle overrides anything written in this file.
4. For each substantial proposal, create a new Intent via `mcp__throne__create_intent(text=..., tags=["throne"])`. Do NOT modify YAML manifest, ADR, or codebase directly.
5. If `missing_kinds` is non-empty, surface that to the user — do not improvise.
6. Return: list of created intent ids with one-line titles, affected areas (system_instructions / skills / metrics), and an explicit note that nothing was activated, only proposed.

Mode: throne.
