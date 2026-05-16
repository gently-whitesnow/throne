#!/usr/bin/env bash
# Realtime contract coverage gate.
#
# For each event name in specs/contracts/realtime/events.yaml, verify:
#   1. there is a corresponding domain-event record in
#      apps/api/src/Throne.Application/Events/IntentEvents.cs
#      (named exactly per the yaml `csharp_event_class` or PascalCase fallback);
#   2. some per-aggregate mapper under apps/api/src/Throne.Api/Realtime/ maps
#      the domain event to RealtimeEventNames.<Pascal>;
#   3. the frontend has at least one useRealtimeEvent("<name>", ...) call under
#      apps/web/src/, outside shared/realtime/.
#
# This makes "halfway" additions impossible: yaml + Application.Events record + Api
# RealtimeDomainEventHandler case + useRealtimeEvent on the frontend must move
# together.
#
# Usage: scripts/quality/realtime-verify-coverage.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

YAML="specs/contracts/realtime/events.yaml"
EVENTS_FILE="apps/api/src/Throne.Application/Events/IntentEvents.cs"
HANDLER_DIR="apps/api/src/Throne.Api/Realtime"
WEB_SRC="apps/web/src"

if [[ ! -f "$YAML" ]]; then
  echo "ERROR: $YAML not found." >&2
  exit 1
fi

# Extract event names + payload kind using node + yaml (already in apps/web deps).
META_RAW="$(node --input-type=module -e "
import { readFileSync } from 'node:fs';
import { parse } from '${ROOT}/apps/web/node_modules/yaml/dist/index.js';
const doc = parse(readFileSync('${YAML}', 'utf8'));
for (const [name, entry] of Object.entries(doc.events ?? {})) {
  process.stdout.write(name + '\n');
}
")"

if [[ -z "$META_RAW" ]]; then
  echo "ERROR: no events parsed from $YAML." >&2
  exit 1
fi

EVENT_COUNT=0

# pascal-case helper: intent.created -> IntentCreated
to_pascal() {
  local name="$1"
  local out=""
  IFS='._-' read -ra parts <<< "$name"
  for part in "${parts[@]}"; do
    out+="$(tr '[:lower:]' '[:upper:]' <<< "${part:0:1}")${part:1}"
  done
  echo "$out"
}

errors=0

while IFS= read -r name; do
  [[ -z "$name" ]] && continue
  EVENT_COUNT=$((EVENT_COUNT + 1))
  pascal="$(to_pascal "$name")"

  # 1. domain event record exists
  if ! grep -qE "record[[:space:]]+${pascal}[[:space:]]*\(" "$EVENTS_FILE"; then
    echo "MISSING domain event record '${pascal}' for event '${name}' in ${EVENTS_FILE}" >&2
    errors=$((errors + 1))
  fi

  # 2. some per-aggregate mapper under HANDLER_DIR maps the domain event to RealtimeEventNames.<Pascal>
  if ! grep -RqE --include='*.cs' "RealtimeEventNames\.${pascal}([^A-Za-z0-9_]|$)" "$HANDLER_DIR"; then
    echo "MISSING handler case for RealtimeEventNames.${pascal} for event '${name}' anywhere under ${HANDLER_DIR}" >&2
    errors=$((errors + 1))
  fi

  # 3. frontend subscriber
  matches=$(grep -RIn --include='*.ts' --include='*.tsx' \
    --exclude-dir='generated' --exclude-dir='realtime' \
    "useRealtimeEvent(\"${name}\"" "$WEB_SRC" 2>/dev/null | wc -l | tr -d ' ')
  if [[ "$matches" -eq 0 ]]; then
    echo "MISSING frontend subscriber useRealtimeEvent(\"${name}\", ...) under ${WEB_SRC}" >&2
    errors=$((errors + 1))
  fi
done <<< "$META_RAW"

if [[ "$errors" -ne 0 ]]; then
  echo "" >&2
  cat >&2 <<EOF
ERROR: realtime contract coverage failed (${errors} issue(s)).

Adding a new realtime event requires four moves in the same change:
  1. specs/contracts/realtime/events.yaml entry
  2. domain event record in Throne.Application/Events/IntentEvents.cs
  3. case in a per-aggregate mapper under Throne.Api/Realtime/ mapping to
     RealtimeEventNames.<PascalName> (Intent*RealtimeMapper, TagRealtimeMapper,
     InstructionPatchRealtimeMapper, DreamRealtimeMapper, …)
  4. useRealtimeEvent("<name>", ...) call in apps/web/src/

The repository raises the event by carrying it on its outcome record; the
domain-event-dispatching unit-of-work decorator fans it out automatically.

See specs/ADR/0008-realtime-contract-first-events.md.
EOF
  exit 1
fi

echo "Realtime contract coverage OK (${EVENT_COUNT} events)."
