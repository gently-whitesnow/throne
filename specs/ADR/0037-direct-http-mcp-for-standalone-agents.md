# ADR-0037: Direct HTTP MCP for standalone agents

## Status

Accepted
Date: 2026-06-13
Related: [ADR-0009](0009-cross-process-realtime-fanout.md), [ADR-0014](0014-mcp-initialize-instructions-routing.md), [ADR-0034](0034-dual-execution-contours-hooks-vs-bundles.md)
Derived from: [ADR-0009](0009-cross-process-realtime-fanout.md)

## Context

[ADR-0034](0034-dual-execution-contours-hooks-vs-bundles.md) makes the embedded terminal the priority execution contour: Throne injects context upfront and derives most status transitions from runtime hooks. The external standalone MCP path remains supported, but it is secondary and less deterministic because the agent must notice the mini-router from MCP `initialize` and call `get_prompt_bundle(mode, intent_id?)`.

[ADR-0009](0009-cross-process-realtime-fanout.md) introduced a thin STDIO to HTTP proxy for two reasons:

- writes had to enter the same `Throne.Api` process that owns the in-memory SSE broker;
- at that time the target MCP clients did not reliably allow a local non-HTTPS HTTP MCP server.

The first reason is satisfied equally well by a direct client connection to `Throne.Api /mcp`: mutations still happen inside the API process, so domain events still reach the same `InMemoryRealtimeBroker` and browser SSE subscribers. The second reason is now a client-compatibility question.

## Decision

Deprecate the repository-local stdio proxy as the default standalone entry point and move standalone setup to direct Streamable HTTP MCP:

```text
http://localhost:5008/mcp
```

The embedded terminal remains the primary documented path. The standalone section stays secondary and must explicitly tell the operator to ask the agent to work through Throne, because the mini-router from [ADR-0014](0014-mcp-initialize-instructions-routing.md) is a best-effort routing hint, not a deterministic lifecycle hook.

The mini-router is not removed. `Throne.Api` still returns `ThroneServerInstructions.MiniRouter` in MCP `InitializeResult.instructions`; direct HTTP clients receive the same instructions that the STDIO proxy forwarded.

Realtime fanout is not weakened. Direct HTTP clients call the same `Throne.Api /mcp` endpoint that the proxy called, so all writes, audit wrapping, domain event dispatch and SSE fanout remain in one process as required by [ADR-0009](0009-cross-process-realtime-fanout.md).

## Client support check

Checked on 2026-06-13.

| Client | Plain HTTP localhost support | Config shape | Notes |
| --- | --- | --- | --- |
| Claude Code CLI | Yes | `claude mcp add --transport http throne http://localhost:5008/mcp`; JSON stores `{ "type": "http", "url": "http://localhost:5008/mcp" }` | Official docs describe `--transport http`, headers, and `streamable-http` as a JSON alias for `http`. Local smoke check with Claude Code 2.1.177 accepted the plain HTTP localhost URL. |
| Claude Desktop | Via bridge | `claude_desktop_config.json`: `{ "command": "npx", "args": ["-y", "mcp-remote", "http://localhost:5008/mcp", "--allow-http"] }` | Desktop-local MCP uses stdio. Direct localhost through a remote custom connector is not the supported path because that connection originates outside the machine. Use the standard `mcp-remote` stdio↔HTTP bridge; `--allow-http` is required for plain HTTP localhost. |
| Cursor | Yes | `~/.cursor/mcp.json` or `.cursor/mcp.json`: `{ "mcpServers": { "throne": { "url": "http://localhost:5008/mcp" } } }`; optional headers are supported for remote servers. | Official Cursor MCP docs document MCP server configuration; Cursor community issue reports use of `type: http`/`url` and an HTTP reconnection bug, so follow-up should verify current IDE behavior against Throne keep-alives. No HTTPS-only requirement was found for localhost. |
| Codex CLI | Yes | `~/.codex/config.toml`: `[mcp_servers.throne] url = "http://localhost:5008/mcp"`; CLI: `codex mcp add throne --url http://localhost:5008/mcp` | Official Codex config reference defines `mcp_servers.<id>.url` as the endpoint for a streamable HTTP MCP server and supports `http_headers` / `env_http_headers`. Local smoke check with codex-cli 0.139.0 accepted the plain HTTP localhost URL. |

Sources:

- Claude Code MCP docs: https://docs.anthropic.com/en/docs/claude-code/mcp
- Claude custom connectors network requirements: https://support.anthropic.com/en/articles/11175166-getting-started-with-custom-connectors-using-remote-mcp
- Cursor MCP docs: https://cursor.com/docs/mcp
- Cursor HTTP reconnect issue to verify in follow-up: https://forum.cursor.com/t/http-mcp-server-becomes-unresponsive-after-repeated-sse-stream-disconnects/152243
- Codex configuration reference: https://developers.openai.com/codex/config-reference
- MCP transport spec: https://modelcontextprotocol.io/specification/2025-03-26/basic/transports

## Consequences

Positive:

- The default standalone setup loses one moving part: no .NET global tool installation, NuGet publish workflow, project-local proxy process or proxy tests.
- The topology stays aligned with [ADR-0009](0009-cross-process-realtime-fanout.md): writes still enter `Throne.Api`, so the in-memory realtime broker remains valid for self-hosted single-instance.
- The mini-router delivery path becomes simpler: clients read instructions directly from the API handshake instead of through a forwarding proxy.

Tradeoffs:

- Claude Desktop cannot be treated as a direct localhost HTTP target via custom connectors. Local Desktop support goes through `mcp-remote`, an external stdio↔HTTP bridge.
- Standalone remains weaker than embedded: the agent can still ignore or underweight `InitializeResult.instructions`. Documentation must tell the operator to explicitly prompt "work through Throne" in standalone sessions.
- Cursor's HTTP transport has had reconnection issues around SSE/keep-alives. Throne already has MCP keep-alive middleware, but the follow-up implementation must smoke-test Cursor against `http://localhost:5008/mcp`.

## Migration plan

1. Rewrite standalone setup docs to lead with direct HTTP MCP for Claude Code, Cursor and Codex.
2. Move Claude Desktop into a bridge section using `npx mcp-remote http://localhost:5008/mcp --allow-http`.
3. Remove the proxy project after docs have a replacement path and prepare NuGet deprecation.
4. Stop publishing the NuGet package and remove CI that exists only for the proxy.
5. Update landing and infra docs to embedded-first plus secondary standalone direct HTTP.
6. Smoke-test `http://localhost:5008/mcp` with Claude Code, Cursor and Codex against a running `Throne.Api`.

## Follow-up slices

- Remove the proxy project, its tests, its publish workflow and `.quality/maintainability-budget.json` references.
- Update `readme.md` standalone setup to direct HTTP MCP.
- Update `throne-infra` landing setup (`site/src/components/Connect.tsx`, `site/src/i18n/messages/en.json`, `site/src/i18n/messages/ru.json`) to embedded-first plus direct HTTP configs.
- Update `throne-infra/README.md` and verify whether the local-only Caddyfile comment needs a wording change.
- Document the Claude Desktop bridge path through `mcp-remote`.
- Deprecate the legacy NuGet package after the replacement docs are released.

## Out of scope

- Rewriting the landing page or readmes in this ADR pass.
- Changing the mini-router or `get_prompt_bundle` flow.
- Adding OAuth/auth to `/mcp`; Throne remains local-first per [ADR-0029](0029-local-first-invariant-and-legacy-auth.md).
