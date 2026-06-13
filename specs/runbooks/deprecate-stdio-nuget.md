# Deprecate the Stdio NuGet package

The repository no longer builds or publishes the old standalone proxy package. Deprecation on nuget.org is an operator action because it requires package-owner credentials.

Use **deprecate**, not **unlist**: existing installations should keep resolving the package and receive the deprecation message.

## nuget.org UI

1. Open the package page on nuget.org while signed in as a package owner.
2. Open **Manage package**.
3. Select every published version.
4. Choose **Deprecate**.
5. Reason: `Legacy`.
6. Message:

```text
Throne standalone clients now connect directly to http://localhost:5008/mcp via Streamable HTTP. Claude Desktop is supported through npx mcp-remote http://localhost:5008/mcp --allow-http. See ADR-0037 and the repository README.
```

## CLI helper

`dotnet nuget` can list sources, push and delete packages, but it does not currently expose a deprecation command. Use the nuget.org UI above for the actual deprecation.

This helper only captures package metadata before and after the UI action.

```bash
#!/usr/bin/env bash
set -euo pipefail

package_id="Throne.Mcp.Stdio"

dotnet nuget search "$package_id" --source https://api.nuget.org/v3/index.json --exact-match --take 100

cat <<'NEXT'
Open nuget.org -> Manage packages -> Throne.Mcp.Stdio -> Deprecation.
Select all versions, reason Legacy, and save the message from this runbook.
Do not unlist the package.
NEXT
```
