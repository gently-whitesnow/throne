#!/usr/bin/env bash
set -euo pipefail

find_checker() {
  local script_dir
  script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)"

  if [[ -f "$script_dir/duplicate_check.py" ]]; then
    printf '%s\n' "$script_dir/duplicate_check.py"
    return
  fi

  printf 'duplicate_check.py не найден рядом с %s.\n' "$0" >&2
  printf 'Скопируйте оба файла maintainability pack scripts в target repo.\n' >&2
  return 66
}

find_python() {
  if command -v python3 >/dev/null 2>&1; then
    printf 'python3\n'
    return
  fi

  if command -v python >/dev/null 2>&1; then
    printf 'python\n'
    return
  fi

  printf 'Python не найден. Для starter duplicate scan нужен python3 или python.\n' >&2
  return 127
}

resolve_repo_root() {
  if [[ -n "${DOTNET_QUALITY_ROOT:-}" ]]; then
    printf '%s\n' "$DOTNET_QUALITY_ROOT"
    return
  fi

  git rev-parse --show-toplevel 2>/dev/null || pwd
}

repo_root="$(resolve_repo_root)"
cd "$repo_root"

python_bin="$(find_python)"
checker="$(find_checker)"

export PYTHONDONTWRITEBYTECODE=1
exec "$python_bin" "$checker" "$@"
