#!/usr/bin/env bash
# Usage:
#   scripts/quality/dotnet-test.sh                    # все тесты
#   scripts/quality/dotnet-test.sh --filter '<expr>'  # любой dotnet test --filter
#   scripts/quality/dotnet-test.sh --unit-only        # ярлык: Category!=Integration
#   scripts/quality/dotnet-test.sh --integration-only # ярлык: Category=Integration
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT/apps/api"

FILTER=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --filter)
      FILTER="${2:-}"
      shift 2
      ;;
    --filter=*)
      FILTER="${1#--filter=}"
      shift
      ;;
    --unit-only)
      FILTER="Category!=Integration"
      shift
      ;;
    --integration-only)
      FILTER="Category=Integration"
      shift
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 2
      ;;
  esac
done

if [[ -n "$FILTER" ]]; then
  exec dotnet test Throne.slnx -c Release --nologo --no-build --filter "$FILTER"
fi
exec dotnet test Throne.slnx -c Release --nologo --no-build
