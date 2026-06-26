#!/usr/bin/env bash
# Local launcher for the single-binary `throne` (ADR-0048): builds the SPA into
# wwwroot and runs the host process so Kestrel serves UI + API + SQLite on one port.
#
# Modes:
#   (default)    dotnet run — framework-dependent, fast iteration.
#   --publish    build the real self-contained single-file binary for this platform
#                and run it (exercises the shipped artifact).
#
# Flags:
#   --no-web         skip `pnpm build` (backend-only iteration; reuse existing wwwroot)
#   --urls <url>     bind address (default from appsettings.json: http://localhost:5008)
#   --                pass everything after it straight to `throne serve`
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

MODE="run"
BUILD_WEB=1
PASSTHRU=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --publish) MODE="publish"; shift ;;
    --no-web)  BUILD_WEB=0; shift ;;
    --urls)    PASSTHRU+=(--urls "$2"); shift 2 ;;
    --)        shift; PASSTHRU+=("$@"); break ;;
    *)         PASSTHRU+=("$1"); shift ;;
  esac
done

# Platform → .NET RID. macOS is wired today; the other rows are laid out so adding
# a platform is a one-line change (the RID must also be in <RuntimeIdentifiers>).
detect_rid() {
  local os arch
  os="$(uname -s)"; arch="$(uname -m)"
  case "$os" in
    Darwin)
      case "$arch" in
        arm64)  echo "osx-arm64" ;;
        x86_64) echo "osx-x64" ;;
        *) return 1 ;;
      esac ;;
    Linux)
      case "$arch" in
        x86_64|amd64) echo "linux-x64" ;;   # untested here, but a published target
        *) return 1 ;;
      esac ;;
    *) return 1 ;;  # Windows (win-x64) — future: run from Git Bash / adapt to PowerShell
  esac
}

if [[ "$BUILD_WEB" -eq 1 ]]; then
  echo "==> Building SPA (apps/web → wwwroot)"
  pnpm -C apps/web build
fi

if [[ "$MODE" == "publish" ]]; then
  RID="$(detect_rid)" || { echo "Unsupported platform $(uname -s)/$(uname -m); add it to detect_rid + <RuntimeIdentifiers>." >&2; exit 1; }
  OUT="$ROOT/out/$RID"
  echo "==> Publishing self-contained single-file binary ($RID)"
  dotnet publish apps/api/src/Throne.Api/Throne.Api.csproj -c Release -r "$RID" -o "$OUT" --nologo
  echo "==> Running $OUT/throne"
  exec "$OUT/throne" serve "${PASSTHRU[@]}"
fi

echo "==> Running host process (dotnet run)"
exec dotnet run --project apps/api/src/Throne.Api -c Release -- serve "${PASSTHRU[@]}"
