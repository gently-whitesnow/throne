#!/usr/bin/env bash
# Single entrypoint for quality verification.
# Usage:
#   scripts/quality/verify.sh           # all gates
#   scripts/quality/verify.sh --fast    # skip security audit (network)
#   scripts/quality/verify.sh --only build|test|format|audit
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

FAST=0
ONLY=""
for arg in "$@"; do
  case "$arg" in
    --fast) FAST=1 ;;
    --only) shift; ONLY="${1:-}" ;;
    --only=*) ONLY="${arg#--only=}" ;;
  esac
done

run_gate() {
  local name="$1"
  local script="$2"
  if [[ -n "$ONLY" && "$ONLY" != "$name" ]]; then
    return 0
  fi
  echo "▶ gate: $name"
  bash "$script"
  echo "✓ gate passed: $name"
}

run_gate "format" "scripts/quality/dotnet-format-verify.sh"
run_gate "build"  "scripts/quality/dotnet-build-warnaserror.sh"
run_gate "test"   "scripts/quality/dotnet-test.sh"

if [[ "$FAST" -eq 0 ]]; then
  run_gate "audit" "scripts/quality/package-audit.sh"
else
  echo "⏭  skipping audit (--fast)"
fi

echo ""
echo "ALL GATES PASSED"
