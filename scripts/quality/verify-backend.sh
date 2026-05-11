#!/usr/bin/env bash
# Backend-only quality verification (thin wrapper над verify.py).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
exec python3 "$ROOT/scripts/quality/verify.py" --scope backend "$@"
