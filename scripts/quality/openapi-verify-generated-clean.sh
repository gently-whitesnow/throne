#!/usr/bin/env bash
# Regenerate backend + frontend artefacts and fail if anything drifted from specs/contracts/.
# Compares working tree to git index for the generated paths.
# Usage: scripts/quality/openapi-verify-generated-clean.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCRIPTS_DIR="$ROOT/scripts/quality"
cd "$ROOT"

GENERATED_PATHS=(
  "apps/api/src/Throne.Intents.Contracts/Generated/"
  "apps/api/src/Throne.Instructions.Contracts/Generated/"
  "apps/api/src/Throne.Api/Generated/"
  "apps/web/src/shared/api/generated/"
)

bash "$SCRIPTS_DIR/openapi-generate.sh"
bash "$SCRIPTS_DIR/codegen-frontend.sh"

echo "==> Checking OpenAPI generated artefacts for drift"

if ! git diff --exit-code -- "${GENERATED_PATHS[@]}"; then
  cat >&2 <<EOF

ERROR: OpenAPI generated files drifted from specs/contracts/ source.
Fix: run scripts/quality/openapi-generate.sh and scripts/quality/codegen-frontend.sh,
then commit updated generated files alongside the YAML change.
EOF
  exit 1
fi

echo "No OpenAPI drift detected."
