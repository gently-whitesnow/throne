#!/usr/bin/env bash
# Local launcher for the single-binary `throne` (ADR-0048): builds the SPA into
# wwwroot and runs the host process so Kestrel serves UI + API + SQLite on one port.
#
# Modes:
#   (default)    dotnet run — framework-dependent, fast iteration.
#   --publish    build the real self-contained single-file binary for this platform
#                and run it (exercises the shipped artifact).
#
# Flags:
#   --no-web                skip `pnpm build` (backend-only iteration; reuse existing wwwroot)
#   --urls <url>            bind address (default from appsettings.json: http://localhost:5008)
#   --remote-db [target]    share the SQLite file across machines via sshfs (single-writer
#                           workflow: take an exclusive remote flock, mount the dir, point
#                           throne at it, force journal_mode=DELETE since WAL is unsafe on
#                           network FS, release the lock + unmount on exit). Target is the
#                           positional arg, $THRONE_REMOTE_DB_SSH, or required-missing.
#   --                      pass everything after it straight to `throne serve`
#
# Env for --remote-db:
#   THRONE_REMOTE_DB_SSH    ssh target (e.g. user@host); required unless passed positionally
#   THRONE_REMOTE_DB_PATH   remote directory holding throne.db (default: /var/lib/throne-db)
#   THRONE_REMOTE_DB_MOUNT  local mount point (default: <repo>/.throne-remote)
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

MODE="run"
BUILD_WEB=1
REMOTE_DB=0
REMOTE_SSH_OVERRIDE=""
PASSTHRU=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --publish) MODE="publish"; shift ;;
    --no-web)  BUILD_WEB=0; shift ;;
    --urls)    PASSTHRU+=(--urls "$2"); shift 2 ;;
    --remote-db)
      REMOTE_DB=1; shift
      # Optional positional ssh target right after the flag (skip if it looks like another flag).
      if [[ $# -gt 0 && "$1" != --* ]]; then
        REMOTE_SSH_OVERRIDE="$1"; shift
      fi
      ;;
    --)        shift; PASSTHRU+=("$@"); break ;;
    *)         PASSTHRU+=("$1"); shift ;;
  esac
done

# Platform → .NET RID. macOS is wired today; the other rows are laid out so adding
# a platform is a one-line change (the RID must also be in <RuntimeIdentifiers>).
detect_rid() {
  local os arch
  os="$(uname -s)"; arch="$(uname -m)"
  case "$os" in
    Darwin)
      case "$arch" in
        arm64)  echo "osx-arm64" ;;
        x86_64) echo "osx-x64" ;;
        *) return 1 ;;
      esac ;;
    Linux)
      case "$arch" in
        x86_64|amd64) echo "linux-x64" ;;   # untested here, but a published target
        *) return 1 ;;
      esac ;;
    *) return 1 ;;  # Windows (win-x64) — future: run from Git Bash / adapt to PowerShell
  esac
}

# --- remote-db wiring ---------------------------------------------------------
# Single-writer SQLite over sshfs: we hold an exclusive flock on the remote host
# for the whole session, so a second laptop trying the same flag fails fast
# instead of corrupting the file. WAL is incompatible with network FS (shared
# memory mmap), so we force journal_mode=DELETE — Throne reads this from
# EfPersistenceOptions.

REMOTE_SSH=""
REMOTE_PATH=""
REMOTE_MOUNT=""
REMOTE_LOCK_PID=""
REMOTE_MOUNTED=0

cleanup_remote() {
  set +e
  if [[ "$REMOTE_MOUNTED" -eq 1 ]]; then
    echo "==> Unmounting $REMOTE_MOUNT"
    if [[ "$(uname -s)" == "Darwin" ]]; then
      umount "$REMOTE_MOUNT" 2>/dev/null || diskutil unmount "$REMOTE_MOUNT" 2>/dev/null
    else
      fusermount -u "$REMOTE_MOUNT" 2>/dev/null || umount "$REMOTE_MOUNT" 2>/dev/null
    fi
  fi
  if [[ -n "$REMOTE_LOCK_PID" ]] && kill -0 "$REMOTE_LOCK_PID" 2>/dev/null; then
    echo "==> Releasing remote lock"
    kill "$REMOTE_LOCK_PID" 2>/dev/null
    wait "$REMOTE_LOCK_PID" 2>/dev/null
  fi
  set -e
}

setup_remote_db() {
  REMOTE_SSH="${REMOTE_SSH_OVERRIDE:-${THRONE_REMOTE_DB_SSH:-}}"
  REMOTE_PATH="${THRONE_REMOTE_DB_PATH:-/var/lib/throne-db}"
  REMOTE_MOUNT="${THRONE_REMOTE_DB_MOUNT:-$ROOT/.throne-remote}"

  if [[ -z "$REMOTE_SSH" ]]; then
    echo "ERROR: --remote-db needs an ssh target. Pass it positionally (--remote-db user@host) or set THRONE_REMOTE_DB_SSH." >&2
    exit 1
  fi

  if ! command -v sshfs >/dev/null 2>&1; then
    cat >&2 <<EOF
sshfs not found. Install it once:
  macOS:  brew install --cask macfuse && brew install gromgit/fuse/sshfs-mac
  Linux:  sudo apt install sshfs
EOF
    exit 1
  fi

  echo "==> Acquiring exclusive remote lock on $REMOTE_SSH:$REMOTE_PATH/throne.lock"
  # Hold the lock for the lifetime of this background ssh. flock -n fails fast if
  # another laptop is already in. The remote `printf ok` lets us confirm
  # acquisition synchronously, then `sleep infinity` keeps the fd open.
  local lock_fifo
  lock_fifo="$(mktemp -u "${TMPDIR:-/tmp}/throne-remote-lock.XXXXXX")"
  mkfifo "$lock_fifo"
  # shellcheck disable=SC2029  # remote-side expansion is intentional
  ssh -o BatchMode=no -o ServerAliveInterval=30 "$REMOTE_SSH" \
    "mkdir -p '$REMOTE_PATH' && flock -n '$REMOTE_PATH/throne.lock' -c 'printf ok; sleep infinity'" \
    > "$lock_fifo" 2>/tmp/throne-remote-lock.err &
  REMOTE_LOCK_PID=$!
  trap cleanup_remote EXIT INT TERM

  # Read the "ok" handshake with a timeout so we don't hang forever if the
  # remote refuses the lock or ssh fails.
  local ack
  if ! ack="$(timeout 15 head -c2 "$lock_fifo")" || [[ "$ack" != "ok" ]]; then
    rm -f "$lock_fifo"
    echo "ERROR: failed to acquire remote lock on $REMOTE_SSH:$REMOTE_PATH/throne.lock" >&2
    echo "Either another machine is using it, or ssh/flock failed:" >&2
    cat /tmp/throne-remote-lock.err >&2 || true
    exit 1
  fi
  rm -f "$lock_fifo"

  mkdir -p "$REMOTE_MOUNT"
  echo "==> Mounting $REMOTE_SSH:$REMOTE_PATH → $REMOTE_MOUNT (sshfs)"
  # reconnect: survive transient drops; Compression=no: SQLite pages aren't compressible
  # and compression adds latency; defer_permissions (macOS): trust remote mode bits.
  local sshfs_opts="reconnect,ServerAliveInterval=15,ServerAliveCountMax=3,Compression=no"
  if [[ "$(uname -s)" == "Darwin" ]]; then
    sshfs_opts="$sshfs_opts,defer_permissions,noappledouble"
  fi
  if ! sshfs -o "$sshfs_opts" "$REMOTE_SSH:$REMOTE_PATH" "$REMOTE_MOUNT"; then
    echo "ERROR: sshfs mount failed" >&2
    exit 1
  fi
  REMOTE_MOUNTED=1

  # Lower into the existing CLI surface: throne resolves --db onto
  # Persistence:Sqlite:DataSource, JournalMode override is read by
  # EfSchemaInitializer at startup.
  PASSTHRU+=(--db "$REMOTE_MOUNT/throne.db" "--Persistence:Sqlite:JournalMode=DELETE")
  echo "==> Remote DB ready: $REMOTE_MOUNT/throne.db (journal_mode=DELETE)"
}

if [[ "$REMOTE_DB" -eq 1 ]]; then
  setup_remote_db
fi

if [[ "$BUILD_WEB" -eq 1 ]]; then
  echo "==> Building SPA (apps/web → wwwroot)"
  pnpm -C apps/web build
fi

if [[ "$MODE" == "publish" ]]; then
  RID="$(detect_rid)" || { echo "Unsupported platform $(uname -s)/$(uname -m); add it to detect_rid + <RuntimeIdentifiers>." >&2; exit 1; }
  OUT="$ROOT/out/$RID"
  echo "==> Publishing self-contained single-file binary ($RID)"
  dotnet publish apps/api/src/Throne.Api/Throne.Api.csproj -c Release -r "$RID" -o "$OUT" --nologo
  echo "==> Running $OUT/throne"
  # In remote-db mode the trap is set; use `exec` only when no cleanup is needed.
  if [[ "$REMOTE_DB" -eq 1 ]]; then
    "$OUT/throne" serve "${PASSTHRU[@]+"${PASSTHRU[@]}"}"
    exit $?
  fi
  exec "$OUT/throne" serve "${PASSTHRU[@]+"${PASSTHRU[@]}"}"
fi

echo "==> Running host process (dotnet run)"
if [[ "$REMOTE_DB" -eq 1 ]]; then
  dotnet run --project apps/api/src/Throne.Api -c Release -- serve "${PASSTHRU[@]+"${PASSTHRU[@]}"}"
  exit $?
fi
exec dotnet run --project apps/api/src/Throne.Api -c Release -- serve "${PASSTHRU[@]+"${PASSTHRU[@]}"}"
