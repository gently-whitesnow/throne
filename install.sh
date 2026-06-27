#!/usr/bin/env bash
# One-command installer for the single-binary `throne` (ADR-0048).
#
#   curl -fsSL https://throne.whitesnow.tech/install | bash
#
# Detects OS/arch → RID, downloads the matching `throne-<rid>.tar.gz` release
# asset, unpacks the bundle (binary + wwwroot/skills/specs) into ~/.throne/app,
# installs a `throne` shim on PATH, and launches the daemon.
#
# Shim — not a symlink — by design: with a shim `Environment.ProcessPath` resolves
# to the real binary in ~/.throne/app, so `throne update`/`restart` (which File.Move
# onto ProcessPath) keep working. A symlink would make `update` overwrite the link
# in ~/.local/bin with a lone binary, stranding wwwroot/skills/specs.
#
# Idempotent: re-running performs a clean reinstall of ~/.throne/app in place.
set -euo pipefail

REPO="gently-whitesnow/throne"
APP_DIR="$HOME/.throne/app"
BIN_DIR="$HOME/.local/bin"
SHIM="$BIN_DIR/throne"
MANUAL_INSTALL="https://github.com/$REPO#запуск"

err() { printf 'throne install: %s\n' "$1" >&2; exit 1; }
info() { printf '%s\n' "$1"; }

require() { command -v "$1" >/dev/null 2>&1 || err "required tool '$1' not found"; }

detect_rid() {
  local os arch
  os="$(uname -s)"
  arch="$(uname -m)"
  case "$os" in
    Darwin)
      case "$arch" in
        arm64) echo "osx-arm64" ;;
        x86_64) echo "osx-x64" ;;
        *) return 1 ;;
      esac ;;
    Linux)
      case "$arch" in
        x86_64) echo "linux-x64" ;;
        *) return 1 ;;
      esac ;;
    *) return 1 ;;
  esac
}

require curl
require tar

rid="$(detect_rid)" || err "unsupported platform $(uname -s)/$(uname -m) (linux-arm64 and Windows are not yet built). Install manually: $MANUAL_INSTALL"

asset="throne-$rid.tar.gz"
url="https://github.com/$REPO/releases/latest/download/$asset"

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

info "Downloading $asset (latest release)…"
curl -fSL --proto '=https' --tlsv1.2 "$url" -o "$tmp/$asset" \
  || err "download failed from $url"

# NB: всегда оборачивай $VAR в ${VAR}, если сразу за ним идёт не-ASCII символ
# (здесь — многобайтный «…»). macOS-bash 3.2 в UTF-8-локали затягивает первый
# байт multibyte-символа в имя переменной → "${APP_DIR}…" парсится как $APP_DIR<байт>,
# которой нет, и при `set -u` падает с "unbound variable". Скобки фиксируют границу имени.
info "Installing into ${APP_DIR}…"
rm -rf "$APP_DIR"
mkdir -p "$APP_DIR"
tar -xzf "$tmp/$asset" -C "$APP_DIR"
[ -f "$APP_DIR/throne" ] || err "archive did not contain a 'throne' binary"
chmod +x "$APP_DIR/throne"

mkdir -p "$BIN_DIR"
cat > "$SHIM" <<'SHIM_EOF'
#!/usr/bin/env bash
exec "$HOME/.throne/app/throne" "$@"
SHIM_EOF
chmod +x "$SHIM"
info "Installed shim at $SHIM"

# Append ~/.local/bin to PATH in the shell rc(s), idempotently. The active shell's
# rc is created if missing; others are touched only when they already exist.
ensure_path() {
  local line='export PATH="$HOME/.local/bin:$PATH"'
  local primary
  case "${SHELL:-}" in
    */zsh) primary="$HOME/.zshrc" ;;
    */bash) primary="$HOME/.bashrc" ;;
    *) primary="$HOME/.profile" ;;
  esac
  local rc
  for rc in "$primary" "$HOME/.zshrc" "$HOME/.bashrc" "$HOME/.profile"; do
    [ "$rc" = "$primary" ] || [ -f "$rc" ] || continue
    if ! grep -qF '.local/bin' "$rc" 2>/dev/null; then
      printf '\n# Added by Throne installer\n%s\n' "$line" >> "$rc"
      info "Added ~/.local/bin to PATH in $rc"
    fi
  done
}
ensure_path

info "Starting Throne…"
"$APP_DIR/throne" || info "Throne did not start cleanly — run '$SHIM logs' for details."

info ""
info "Done. The 'throne' command is available in new shell sessions."
info "In this shell, start it now with: $SHIM"
