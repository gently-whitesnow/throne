#!/usr/bin/env bash
# Regenerate backend + frontend artefacts and fail if codegen changes generated files.
# This compares a filesystem snapshot before/after codegen, not the working tree against git index:
# generated files may be intentionally modified but not staged yet.
# Usage: scripts/quality/openapi-verify-generated-clean.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCRIPTS_DIR="$ROOT/scripts/quality"
cd "$ROOT"

GENERATED_PATHS=(
  "apps/api/src/Throne.Intents.Contracts/Generated/"
  "apps/api/src/Throne.Instructions.Contracts/Generated/"
  "apps/api/src/Throne.Tags.Contracts/Generated/"
  "apps/api/src/Throne.Dream.Contracts/Generated/"
  "apps/api/src/Throne.Me.Contracts/Generated/"
  "apps/api/src/Throne.ChatUploads.Contracts/Generated/"
  "apps/api/src/Throne.Realtime.Contracts/Generated/"
  "apps/api/src/Throne.Api/Generated/"
  "apps/web/src/shared/api/generated/"
  "apps/web/src/shared/realtime/generated/"
)

SNAPSHOT_DIR="$(mktemp -d)"
cleanup() {
  rm -rf "$SNAPSHOT_DIR"
}
trap cleanup EXIT

snapshot_path() {
  local path="$1"
  local dst="$SNAPSHOT_DIR/$path"

  mkdir -p "$(dirname "$dst")"
  if [[ -d "$path" ]]; then
    mkdir -p "$dst"
    cp -a "$path/." "$dst/"
  elif [[ -e "$path" ]]; then
    cp -a "$path" "$dst"
  fi
}

for path in "${GENERATED_PATHS[@]}"; do
  snapshot_path "$path"
done

bash "$SCRIPTS_DIR/openapi-generate.sh"
bash "$SCRIPTS_DIR/codegen-frontend.sh"

echo "==> Checking OpenAPI generated artefacts for drift"

DRIFT=0
for path in "${GENERATED_PATHS[@]}"; do
  before="$SNAPSHOT_DIR/$path"
  if [[ -e "$before" && -e "$path" ]]; then
    if ! diff -qr "$before" "$path" >/dev/null; then
      echo "Generated path changed after codegen: $path" >&2
      diff -ru "$before" "$path" || true
      DRIFT=1
    fi
  elif [[ -e "$before" || -e "$path" ]]; then
    echo "Generated path presence changed after codegen: $path" >&2
    diff -ru "$before" "$path" || true
    DRIFT=1
  fi
done

if [[ "$DRIFT" -ne 0 ]]; then
  cat >&2 <<EOF

ERROR: OpenAPI generated files drifted from specs/contracts/ source.
Fix: run scripts/quality/openapi-generate.sh and scripts/quality/codegen-frontend.sh,
then commit updated generated files alongside the YAML change.
EOF
  exit 1
fi

echo "No OpenAPI drift detected."
