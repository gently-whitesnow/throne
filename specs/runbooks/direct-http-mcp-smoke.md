# Direct HTTP MCP smoke checklist

Operator-run checklist for validating standalone clients against a running local `Throne.Api`.

## Prerequisites

1. Start Throne with `Throne.Api` listening on `http://localhost:5008`.
2. Open `http://localhost:5008/mcp` only through an MCP client; a browser GET is not a meaningful smoke test.
3. Use a disposable Intent and ask the agent explicitly: "Work through Throne; read the work prompt bundle for this intent before acting."

## Claude Code

```bash
claude mcp add --transport http throne http://localhost:5008/mcp
claude
```

Inside Claude Code:

```text
/mcp
```

Expected: server `throne` is connected, Throne tools are visible, and `get_prompt_bundle` can be called.

## Codex

Config path: `~/.codex/config.toml`.

```toml
[mcp_servers.throne]
url = "http://localhost:5008/mcp"
```

CLI alternative:

```bash
codex mcp add throne --url http://localhost:5008/mcp
```

Expected: a new Codex session exposes Throne MCP tools and can call `get_prompt_bundle`.

## Cursor

Config path: `~/.cursor/mcp.json` or project-local `.cursor/mcp.json`.

```json
{
  "mcpServers": {
    "throne": {
      "url": "http://localhost:5008/mcp"
    }
  }
}
```

Restart Cursor or reconnect the MCP server from settings.

Expected: Throne tools are listed. Keep the IDE idle for a few minutes, then call a read tool again to catch HTTP keep-alive/reconnect regressions.

## Claude Desktop

Config path: `~/Library/Application Support/Claude/claude_desktop_config.json` on macOS or `%APPDATA%\Claude\claude_desktop_config.json` on Windows.

```json
{
  "mcpServers": {
    "throne": {
      "command": "npx",
      "args": [
        "-y",
        "mcp-remote",
        "http://localhost:5008/mcp",
        "--allow-http"
      ]
    }
  }
}
```

Expected: Claude Desktop starts the bridge and sees Throne tools. `--allow-http` is required for plain HTTP localhost. If local policy blocks the bridge, use a tunnel or prefer embedded terminal / Claude Code CLI.
