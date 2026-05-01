#!/usr/bin/env bash
# Generate TypeScript artefacts from OpenAPI contracts (specs/contracts/*) via openapi-typescript.
# Usage: scripts/quality/codegen-frontend.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
WEB="$ROOT/apps/web"

cd "$WEB"

if [[ ! -d node_modules ]]; then
  CI=true pnpm install --frozen-lockfile --prefer-offline
fi

pnpm codegen
echo "Frontend OpenAPI codegen complete."
