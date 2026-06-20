# ADR-0042: OpenCode Shared `serve` + `attach` Front

## Status

Accepted (amends [ADR-0026](0026-embedded-terminal-capabilities-and-run-preflight.md) for the
OpenCode vendor)

## Context

OpenCode's initial-prompt delivery was already moved off the TUI command bus onto the server
session API (`POST /session`, `POST /session/{id}/prompt_async`) because the old
`tui/append-prompt`+`submit-prompt` path raced the front's command-bus subscription: append
returned `true` while the front silently dropped the text. That fixed *delivery*.

A second instance of the same race remained in *display*. The single spawned process was both the
HTTP server and the TUI front (`opencode --hostname --port`). After creating the session Throne
pushed `tui/select-session` to focus the front on it — another fire-and-forget command-bus push.
If the front had not yet subscribed the command was dropped, yet HTTP still returned `true`. The
session ran to completion on the server while the operator stared at a composer parked on a
different, empty session («ничего не происходит», reproduced live 2026-06-20 on `ses_11aadf1fb`).

The class of bug is «server pushes a command the front may not be listening for yet». The cure that
worked for delivery — the front *pulls* by id instead of the server *pushing* — has a direct analogue
for display: `opencode attach <url> --session <id>` pulls the session by id at startup. But `attach`
requires a separate already-running server to attach to, so the server and the front must be split
into two processes.

Empirically (opencode 1.17.7): a *single* `opencode serve` resolves each workspace's local
`opencode.json` provider/model/MCP config per request via `?directory=<ws>` — verified that the
`throne-local` provider and its model map surface for a workspace dir while absent for an unrelated
dir. `prompt_async` accepts an explicit `model={providerID,modelID}` and returns 204 for a
workspace-scoped provider/model. So one shared serve can host every intent's session, and the model
must be pinned on the prompt body (the attach front carries no model flag).

Alternatives considered: (a) keep retrying `select-session` until an indirect signal confirms focus
— no front-state read-back exists, so «confirmation» stays the same race, only softened; (b) a
per-intent serve in its own tmux session — closer to a literal reading but doubles tmux sessions and
needs list-scan/kill plumbing so serve sessions aren't mistaken for intents; (c) serve as a
Throne-hosted background process — simplest DI but dies on a Throne restart, breaking ADR-0026's
«tmux is the single source of truth for liveness».

## Decision

For the OpenCode vendor, split execution from display:

- One shared persistent `opencode serve` per host runs in a fixed-name tmux session
  (`throne-opencode-serve`, reserved pseudo-intent id) at a configured address
  (`Throne:Run:OpencodeServeHostname`/`OpencodeServePort`, default `127.0.0.1:4096`).
  `IOpencodeServeGateway.EnsureRunningAsync` is the idempotent gate every OpenCode spawn passes:
  healthy ⇒ no-op; missing ⇒ spawn + wait for `/global/health`; alive-but-unhealthy ⇒ kill the
  stale session and respawn. A `SemaphoreSlim` serialises spawn so concurrent runs don't
  double-spawn. Because it lives in tmux it survives a Throne restart; because it is host-scoped it
  is never killed on intent `done`. `TmuxSessionName.TryIntentId` excludes the reserved id so
  liveness scans never surface the serve as an intent.
- The per-intent tmux session — the pane the operator watches via the stream bridge — runs only
  `opencode attach <url> --session <id> --dir <ws>`. The agent loop lives in the shared serve, so
  the front can be slow, killed or restarted without losing the run.
- Spawn ordering inverts for this vendor (`INativeSessionInitializer`): the session is created and
  the prompt submitted (model pinned via `prompt_async model={throne-local,modelId}`) *before* the
  pane spawns, and the returned `attach` argv is folded into the spawn. An empty task boots the
  front bare (`attach … --dir` with no `--session`). The vendor spawn argv carries no model/server
  flags (`BuildBaseArgs` is empty). `tui/select-session` is gone.

Auth is unchanged: `OPENCODE_SERVER_PASSWORD`/`_USERNAME` drive a Basic header on Throne's HTTP
calls and are read by the `serve`/`attach` CLIs from the inherited environment.

## Consequences

### Positive

- The display race is structurally gone: the front pulls the session by id at startup, with nothing
  to push and nothing to drop — the same principle that fixed delivery, applied to display.
- The agent loop survives a slow/restarted/killed front and a Throne restart (serve in tmux).
- One serve for all intents (sessions already share `~/.local/share/opencode/opencode.db`); the
  per-intent lifecycle (kill-on-done, liveness scan, list filter) is untouched — still one tmux
  session per intent.

### Negative / Risks

- A second class of long-lived tmux session exists (the host-scoped serve). It is intentionally not
  reaped on intent `done`; it is reclaimed only by killing `throne-opencode-serve` or the tmux
  server.
- The serve binds a fixed configured port; a conflict surfaces as a health-wait timeout on the next
  OpenCode spawn (configurable to dodge).
- The model is now pinned per prompt rather than by a launch flag — correct only as long as the
  workspace `opencode.json` provider id stays `throne-local`.
