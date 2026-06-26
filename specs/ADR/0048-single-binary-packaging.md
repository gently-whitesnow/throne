# ADR-0048: Single-binary packaging — self-contained `throne`

## Status

Accepted
Date: 2026-06-26
Supersedes: [ADR-0027](0027-runtime-model-native-host-process.md) (two-runtime model collapsed into one host-only binary)
Related: [ADR-0026](0026-embedded-terminal-capabilities-and-run-preflight.md) (capabilities), [ADR-0043](0043-static-operational-skills-and-mcp-removal.md) (skills as repo files), [ADR-0047](0047-sqlite-ef-core-persistence.md) (SQLite/EF Core)

## Context

ADR-0027 split Throne into two runtimes: a containerized stack (api + web + mongo) where host-фичи are OFF, and a "host-backend" mode where the API runs natively via `dotnet run` while web stays in an nginx container reverse-proxying to the host. That model carried real cost:

- **Two ways to run** — the only mode that actually unlocks the cockpit (terminal/Run/IDE/`gh`) is the advanced host-backend one; the container default is a degraded shell. Operators had to understand the split, pick a profile, and wire `host.docker.internal`/`extra_hosts`/`ASPNETCORE_URLS=0.0.0.0`.
- **Docker/nginx/Mongo surface** — `docker-compose*.yml`, two Dockerfiles, an nginx `envsubst` template and a `HostRoot` path-translation existed only to feed a container host-state it never really had. ADR-0047 already removed Mongo as a store (SQLite/EF Core), leaving the container stack without its database rationale.
- **Distribution gap** — there was no artifact to hand a user: "install .NET 10 SDK and `dotnet run`" is fine for the advanced audience but was explicitly listed as out-of-scope packaging debt in ADR-0027.

The runtime is now uniform: a single host process serving SPA + API + SQLite. The remaining question is purely packaging — how to ship and update it.

## Subdomain classification

Packaging/runtime-shell — **generic, impl-volatile** (infra/delivery, not business domain). No new business module; no Subdomain map change.

## Volatility check

**Accidental.** Source of pressure = the harness/delivery shape, not the domain: the two-runtime model and the Docker/nginx/Mongo scaffolding were migration tail from the container-first epic premise (ADR-0027), now obsolete after SQLite (ADR-0047) and host-only capabilities. Fixed at the delivery layer (one binary + release pipeline), not by patching code symptoms.

## Decision

Throne ships as **one self-contained, single-file `throne` executable**. Kestrel serves the SPA from `wwwroot` (static files + SPA fallback), the HTTP API, and SQLite in a single process. No Docker, nginx, Mongo, Node, or .NET SDK at runtime.

### 1. Build & publish (per-RID)

`dotnet publish apps/api/src/Throne.Api/Throne.Api.csproj -c Release -r <rid>` produces a self-contained single-file binary named `throne` (csproj sets `AssemblyName=throne`, `PublishSingleFile`, `SelfContained`, `IncludeNativeLibrariesForSelfExtract` when a RID is passed). Targets: `osx-arm64`, `osx-x64`, `linux-x64`, `win-x64`. `InvariantGlobalization=true` (no ICU dependency). **Trimming stays OFF** — ASP.NET Core + EF Core rely on reflection and are not trim-safe; a smaller binary is not worth runtime breakage. **Single-file compression stays OFF** — `EnableCompressionInSingleFile` corrupts memory and crashes the app with `AccessViolationException` on osx-arm64 (dotnet/runtime#123324, reproduces on .NET 7–10); a startable binary beats a smaller one.

The SPA is built first (`pnpm -C apps/web build`); csproj links `apps/web/dist` into `wwwroot`. All RIDs cross-publish from a single Linux runner — no per-OS runners.

### 2. SPA next to the binary, not embedded

The SPA, `skills/`, `specs/manifest/`, and `bin/throne-*` travel **next to** the binary (published as `Content`, reachable via `AppContext.BaseDirectory`), not embedded inside it. Single-file packaging does not bundle `Content` items into the executable — only managed assemblies/native libs self-extract — so embedding the SPA would require a custom embedded-resource layer. Files-next-to-binary keeps the existing `AppContext.BaseDirectory/wwwroot` (and skills/specs) path lookup unchanged and lets `throne update` swap them atomically as a unit.

### 3. `throne update` — self-update from GitHub Releases

`throne update` reads the `latest` GitHub Release of `gently-whitesnow/throne` (override via `THRONE_UPDATE_REPO`), picks the asset for the current RID named `throne-<rid>.tar.gz` (`throne-win-x64.zip` for Windows), downloads it, and **atomically swaps** the install directory (binary + `wwwroot` + `skills` + `specs`). Flags: `--force` (skip the up-to-date check), `--restart` (relaunch the new binary after swap).

### 4. Release pipeline (tag-driven)

`.github/workflows/release.yml` triggers on a pushed `v*` tag (`permissions: contents: write`). A matrix job per RID on `ubuntu-latest` builds the SPA once, cross-publishes the single-file binary, and bundles `out/<rid>` (binary + wwwroot + skills + specs + bin) as `throne-<rid>.tar.gz` (`.zip` for win-x64). Asset names match exactly what `throne update` looks for. A final `release` job downloads all matrix bundles and publishes one GitHub Release for the tag via `softprops/action-gh-release@v2`.

### 5. Host-capabilities default-on by live probe

The two-runtime opt-in is gone, so the "host-фичи require host-backend mode" gate (ADR-0027 §4) disappears. host-фичи (embedded terminal/tmux, Run, Open in IDE, `gh`/`git`) are **default-on by live capability-probe**: a feature lights up when its CLI (`claude`/`codex`/`gh`/`git`/`tmux`/`code`) is detected in `PATH`. There is no container vs host runtime axis and no per-feature enable-toggle in `/settings` (see the ADR-0026 amendment of 2026-06-26). Default URL: `http://localhost:5008` (`appsettings.json` `Urls`; override via `ASPNETCORE_URLS` / `--urls`).

## Consequences

### Positive

- **One way to run.** Download `throne`, run it, get UI+API+SQLite on `http://localhost:5008`. No profiles, no `host.docker.internal`, no SDK at runtime.
- **Smaller surface.** `docker-compose*.yml`, both Dockerfiles, the nginx template, and `HostRoot` translation are deleted. Fewer moving parts to document and secure.
- **First-class distribution + self-update.** Tagged releases produce per-RID artifacts; `throne update` keeps installs current without a package manager.
- **host-фичи "just work"** when the relevant CLI is installed — detection == intent, matching the embedded-only reality (ADR-0026 §9).

### Negative / Risks

- **Larger binaries.** Self-contained, no trimming, and no single-file compression (the latter crashes on osx-arm64, see §1) ship the full runtime per RID (~130 MB). Accepted: a binary that reliably starts outweighs size.
- **Cross-publish trust.** All RIDs are produced from a Linux runner; macOS binaries are unsigned/un-notarized (Gatekeeper friction on first run). Out of scope here.
- **`PATH`-driven capabilities are implicit.** A feature silently stays dark if its CLI isn't on `PATH`; readiness is surfaced in `/settings` ("Готовность") rather than via a toggle the user flips.

## Out of scope

- **Tauri / desktop app** — same stance as ADR-0027 out-of-scope; a possible future intent, not this slice.
- **Separate .NET global tool** (`dotnet tool install`) — rejected: requires the .NET SDK/runtime on the target and a NuGet feed; the self-contained binary needs neither.
- **Embedding the SPA inside the binary** — rejected (§2): single-file does not bundle `Content`; files-next-to-binary is simpler and keeps `AppContext.BaseDirectory` lookups intact.
- **Code signing / notarization** of macOS and Windows binaries — known gap, deferred.
- **Windows parity of the embedded terminal** (`tmux` is unix-only) — unchanged known gap from ADR-0027.
