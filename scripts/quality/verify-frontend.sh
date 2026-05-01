#!/usr/bin/env bash
# Frontend-only quality verification.
# Usage:
#   scripts/quality/verify-frontend.sh
#   scripts/quality/verify-frontend.sh --fast
#   scripts/quality/verify-frontend.sh --only deps|format|lint|typecheck|architecture|test|build|audit
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WEB="$ROOT/apps/web"
cd "$WEB"

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
  shift
  if [[ -n "$ONLY" && "$ONLY" != "$name" ]]; then
    return 0
  fi
  echo "▶ frontend gate: $name"
  "$@"
  echo "✓ frontend gate passed: $name"
}

run_gate "deps" env CI=true pnpm install --frozen-lockfile --prefer-offline
run_gate "format" pnpm format:check
run_gate "lint" pnpm lint
run_gate "typecheck" pnpm typecheck
run_gate "architecture" pnpm architecture
run_gate "test" pnpm test
run_gate "build" pnpm build

if [[ "$FAST" -eq 0 ]]; then
  run_gate "audit" pnpm audit --audit-level high
elif [[ -z "$ONLY" || "$ONLY" == "audit" ]]; then
  echo "⏭  skipping frontend audit (--fast)"
fi

echo ""
echo "FRONTEND GATES PASSED"
