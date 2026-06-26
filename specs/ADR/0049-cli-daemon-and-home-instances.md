# ADR-0049: CLI UX — detached daemon and per-home instances

## Status

Accepted
Date: 2026-06-26
Related: [ADR-0048](0048-single-binary-packaging.md) (single-binary packaging), [ADR-0047](0047-sqlite-ef-core-persistence.md) (SQLite path)

## Context

ADR-0048 collapsed Throne into one `throne` binary, but the CLI it shipped was the
bare minimum: `throne` (= foreground serve), `throne update`, `throne version`. Running
it meant a blocked terminal; there was no stop/status/logs, no way to run an isolated
instance, and no way for an in-session agent to bring Throne up to debug its own change
without colliding with the operator's running instance. The product is unpublished, so
there is no backward-compatibility to keep — the surface can be designed cleanly.

Two design questions drove this:

- **Instance isolation.** How does a second instance (an agent's, a throwaway) avoid the
  operator's database/port/pid? Options: profiles + multi-pid bookkeeping, or relocating
  one state directory. The latter is the established pattern (`GH_CONFIG_DIR`,
  `DOCKER_CONFIG`, `KUBECONFIG`, `OLLAMA_MODELS`) — one knob, no new concept.
- **Process management.** Unix daemonization without a service manager (systemd/launchd is
  out of scope) needs detach + a pid file + graceful stop. The BCL gives neither `setsid`
  nor SIGTERM, so a thin libc P/Invoke is required.

## Subdomain classification

Packaging/runtime-shell — **generic, impl-volatile** (delivery surface, not business
domain). Same classification as ADR-0048; no new business module, no Subdomain map change.

## Volatility check

**Essential.** Driven by a product requirement (a polished start/stop/status/logs/isolation
UX for humans and in-session agents), not harness pressure. The daemon/home shape is fixed
at the CLI layer over the existing ASP.NET config, not by patching a symptom.

## Decision

`throne` grows a full process-management surface, all scoped to a **home** — the state
directory that defines one instance.

1. **Per-home instances.** Default home `~/.throne`; relocate the whole state directory
   with `--home <dir>` or `THRONE_HOME`. Under home live `throne.db`, `throne.pid`,
   `throne.daemon.json` (status metadata), `throne.log`, and `workspaces/`. The SQLite and
   workspace paths default under home **only when the home is explicitly relocated**, so the
   unrelocated default keeps the appsettings-driven `~/.throne/throne.db` /
   `~/.throne/workspaces` behaviour untouched. `stop`/`restart`/`status`/`logs` act on the
   current home's pid.

2. **Detached daemon (unix-first).** Bare `throne` spawns a detached `throne serve` child,
   records its pid/state, waits on `/health`, prints the URL and opens the browser. The child
   is spawned through `/bin/sh` so its stdout/stderr are redirected to `throne.log` (and stdin
   to `/dev/null`) **before exec** — the launcher's inherited pipe is released, so a caller
   that captures output (`out=$(throne …)` or `throne … | …`) gets EOF instead of blocking on
   the long-lived daemon. The child `setsid`s itself (libc P/Invoke) so closing the launching
   terminal does not kill it. `stop` sends SIGTERM (libc `kill`) with a SIGKILL fallback.
   Double-start is refused with "already running at <url>". `-a/--attach` keeps the host in the
   foreground (Ctrl-C stops); a foreground instance still registers its pid/state so
   `status`/`stop` work against it. Windows and `dotnet run` fall back to foreground (detached
   daemon is a later best-effort slice).

3. **Faithful restart.** The instance persists its resolved launch config (host args) in
   `throne.daemon.json`, so `throne restart` — and the relaunch behind `throne update --restart`
   — replay the *original* port/db/workspace regardless of the flags passed to restart (an
   explicit `-p` overrides just the port). `update --restart` thus restarts the running daemon
   onto the freshly installed binary; the download/swap/asset logic (ADR-0048 §3) is unchanged.

4. **Human aliases over existing config.** `-p/--port` lowers onto `--urls`, `--db` onto
   `Persistence:Sqlite:DataSource`, `--home` onto the pid/log/db/workspace defaults — all via
   command-line config, which already wins over env/appsettings. The lowering is idempotent so
   the daemon child re-parsing already-resolved args never clobbers a custom `--db`. No new
   config system.

5. **Browser auto-open with an agent-safe detector.** The URL is always printed. The browser
   opens on start (bare and `-a`) **except** when stdout is not a TTY (the typical agent/pipe
   signature), `--no-browser`/`THRONE_NO_BROWSER` is set, or a known CI variable is present.
   A failed open never fails the launch.

6. **Agent isolation recipe.** `THRONE_HOME=<repo>/.throne-agent throne -p 5009` brings up an
   isolated instance (own db/pid/workspaces, non-TTY ⇒ no browser); `THRONE_HOME=… throne stop`
   tears it down. Documented in `readme.md`/`ROOT.md`.

## Consequences

### Positive

- **Polished lifecycle.** start/stop/restart/status/logs feel like a real dev tool; the
  terminal is freed by default.
- **One-knob isolation.** A second instance is one env var away; no profiles, no multi-pid.
- **Agent-friendly.** Non-TTY suppresses the browser automatically; an agent can stand up and
  debug its own Throne without touching the operator's instance.

### Negative / Risks

- **Unix-only daemon.** Windows detach/stop is deferred; commands report the limitation
  honestly and fall back to foreground.
- **pid reuse.** Liveness is pid-based; a recycled pid is a theoretical false-positive on a
  single-user local box. Accepted.
- **Self-contained log rotation.** A single size-capped roll to `throne.log.1`, no log
  framework — enough for one run, not a retention policy.

## Out of scope

- Install as a system service (systemd/launchd/Windows service) — unchanged from ADR-0048.
- Full Windows daemon parity.
- Changes to `throne update`'s download/swap/asset logic (ADR-0048 §3 stands); only its
  `--restart` relaunch now goes through the daemon restart instead of a raw foreground serve.
