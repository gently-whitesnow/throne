#!/usr/bin/env bash
# Backend-only quality verification.
# Usage:
#   scripts/quality/verify-backend.sh
#   scripts/quality/verify-backend.sh --fast
#   scripts/quality/verify-backend.sh --only contracts|format|build|test|audit
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

FAST=0
ONLY=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --fast)
      FAST=1
      shift
      ;;
    --only)
      ONLY="${2:-}"
      shift 2
      ;;
    --only=*)
      ONLY="${1#--only=}"
      shift
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 2
      ;;
  esac
done

run_gate() {
  local name="$1"
  local script="$2"
  if [[ -n "$ONLY" && "$ONLY" != "$name" ]]; then
    return 0
  fi
  echo "▶ backend gate: $name"
  bash "$script"
  echo "✓ backend gate passed: $name"
}

run_gate "contracts" "scripts/quality/openapi-verify-generated-clean.sh"
run_gate "realtime" "scripts/quality/realtime-verify-coverage.sh"
run_gate "format" "scripts/quality/dotnet-format-verify.sh"
run_gate "build" "scripts/quality/dotnet-build-warnaserror.sh"
run_gate "test" "scripts/quality/dotnet-test.sh"

if [[ "$FAST" -eq 0 ]]; then
  run_gate "audit" "scripts/quality/package-audit.sh"
elif [[ -z "$ONLY" || "$ONLY" == "audit" ]]; then
  echo "⏭  skipping backend audit (--fast)"
fi

echo ""
echo "BACKEND GATES PASSED"
