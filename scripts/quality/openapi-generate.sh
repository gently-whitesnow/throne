#!/usr/bin/env bash
# Generate .NET artefacts from OpenAPI contracts (specs/contracts/*) via NSwag.
# Iterates over every nswag.*.json config in apps/api/.
# Usage: scripts/quality/openapi-generate.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
API_DIR="$ROOT/apps/api"

cd "$API_DIR"

if [[ ! -f ".config/dotnet-tools.json" ]]; then
  echo "ERROR: $API_DIR/.config/dotnet-tools.json not found." >&2
  exit 1
fi

dotnet tool restore >/dev/null

shopt -s nullglob
configs=( nswag.*.json )
shopt -u nullglob

if [[ ${#configs[@]} -eq 0 ]]; then
  echo "No nswag.*.json configs in $API_DIR." >&2
  exit 1
fi

for config in "${configs[@]}"; do
  echo "==> NSwag: $config"
  dotnet tool run nswag run "$config"
done

echo "Backend OpenAPI codegen complete."
