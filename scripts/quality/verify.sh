#!/usr/bin/env bash
# Single entrypoint for quality verification.
# Usage:
#   scripts/quality/verify.sh                           # all gates
#   scripts/quality/verify.sh --fast                    # skip security audits
#   scripts/quality/verify.sh --scope backend|frontend  # selected app family
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

FAST=0
SCOPE="all"
while [[ $# -gt 0 ]]; do
  case "$1" in
    --fast)
      FAST=1
      shift
      ;;
    --scope)
      SCOPE="${2:-}"
      shift 2
      ;;
    --scope=*)
      SCOPE="${1#--scope=}"
      shift
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 2
      ;;
  esac
done

run_verify() {
  local script="$1"
  if [[ "$FAST" -eq 1 ]]; then
    bash "$script" --fast
  else
    bash "$script"
  fi
}

case "$SCOPE" in
  all)
    run_verify scripts/quality/verify-backend.sh
    run_verify scripts/quality/verify-frontend.sh
    ;;
  backend)
    run_verify scripts/quality/verify-backend.sh
    ;;
  frontend)
    run_verify scripts/quality/verify-frontend.sh
    ;;
  *)
    echo "Unknown scope: $SCOPE" >&2
    exit 2
    ;;
esac

echo ""
echo "ALL GATES PASSED"
