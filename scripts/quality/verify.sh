#!/usr/bin/env bash
# Single quality verify entrypoint.
# Реальная логика — в verify.py; этот файл сохранён ради совместимости со
# скриптами/документацией, ссылающимися на bash-обёртку.
#
# Usage:
#   scripts/quality/verify.sh                           # все включённые gates
#   scripts/quality/verify.sh --fast                    # без slow gates (~1 мин)
#   scripts/quality/verify.sh --scope backend|frontend  # одна сторона
#   scripts/quality/verify.sh --only backend-format     # один gate
#   scripts/quality/verify.sh --skip backend-audit      # без конкретного gate
#   scripts/quality/verify.sh --list                    # перечислить gates
#   scripts/quality/verify.sh --dry-run                 # план без запуска
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
exec python3 "$ROOT/scripts/quality/verify.py" "$@"
