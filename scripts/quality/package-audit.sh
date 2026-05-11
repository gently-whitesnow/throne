#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT/apps/api"

OUTPUT=$(dotnet list Throne.slnx package --vulnerable --include-transitive 2>&1 || true)
echo "$OUTPUT"

if echo "$OUTPUT" | grep -qiE "has the following vulnerable packages"; then
  echo "✗ vulnerable packages detected"
  exit 1
fi

echo "✓ no vulnerable packages"
