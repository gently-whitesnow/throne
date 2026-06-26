# Throne

Кокпит цикла разработки вокруг намерения (`Intent`) для человека в связке с AI-агентами. Web UI + embedded agent terminal + static operational skills.

## Миссия

Throne — кокпит цикла разработки для человека, работающего в связке с AI-агентами. Вокруг намерения (Intent) он сводит в одно окно постановку, память и предпочтения, запуск агента, репозитории и ревью результата — и учится на каждом диалоге, чтобы следующий проход был ближе к ожиданиям.

## Контекст

- **Кто пользователь:** человек, который сочетает свои сильные стороны (вкус, воля, семантическое понимание, persistent memory) с сильными сторонами AI (пропускная способность, неутомимость, механическая согласованность).
- **Аудитория:** инфраструктура для класса ai-разработчиков, не персональный инструмент одного автора.
- **Первичный артефакт:** предпочтения человека. Решения и командные артефакты — в будущем.
- **Намерения (Intent):** единица намерения, куда приносится работа — руками или автоматически из jira, мессенджеров, багтрекеров. Вокруг неё Throne сводит постановку, запуск агента, репозитории и ревью результата.
- **Граница:** Throne оркестрирует инструменты и агентов вокруг намерения, но не подменяет их собой: код пишут агенты в своих средах, правит человек в своём IDE. Throne — это управляющий контур (постановка → агент → PR → ревью → доработка), а не ещё один редактор или агент.
- **Цикл улучшения:** агент по запросу разбирает локальные диалоги → присылает патчи в Throne → человек применяет их → следующий результат ближе к ожиданиям, чем предыдущий.
- **Цель:** усилить человека. Оцифровка опыта — побочный эффект того, что система помнит предпочтения и решения.

## Запуск

Throne — это один self-contained бинарь `throne`: Kestrel в одном процессе отдаёт UI (SPA), API и SQLite. Никаких Docker/Mongo/Node/.NET SDK в рантайме. Throne работает локально, без облака и без сетевой авторизации. Основной путь — embedded-терминал в UI: Throne сам запускает агента, передаёт ему контекст, прикладывает operational skills и ведёт lifecycle через hooks. Внешний MCP endpoint удалён; dogfooding вне UI делается теми же статическими skills из репозитория через `skills/<id>/bin/throne-*` CLI (см. [ADR-0043](specs/ADR/0043-static-operational-skills-and-mcp-removal.md)). Упаковка — [ADR-0048](specs/ADR/0048-single-binary-packaging.md).

### 1. Получить бинарь

**Готовый.** Скачай `throne` под свою платформу из [GitHub Releases](https://github.com/gently-whitesnow/throne/releases) (ассеты `throne-<rid>.tar.gz`, для Windows — `throne-win-x64.zip`; RID: `osx-arm64`, `osx-x64`, `linux-x64`, `win-x64`), распакуй — внутри лежат бинарь и рядом `wwwroot`/`skills`/`specs`.

**Собрать самому.** Нужен .NET 10 SDK и pnpm только на сборку, не на запуск:

```bash
git clone https://github.com/gently-whitesnow/throne
cd throne
pnpm -C apps/web build   # UI попадает в wwwroot рядом с бинарём
dotnet publish apps/api/src/Throne.Api/Throne.Api.csproj -c Release -r <rid>
```

`<rid>` — один из `osx-arm64` / `osx-x64` / `linux-x64` / `win-x64`. На выходе — single-file бинарь `throne` рядом с `wwwroot`/`skills`/`specs`.

### 2. Запустить

```bash
./throne          # или ./throne serve — поднимает UI+API+SQLite в одном процессе
```

Открой `http://localhost:5008`. Порт переопределяется через `ASPNETCORE_URLS` или флаг `--urls` (например `./throne --urls http://localhost:9000`).

Дефолты подобраны так, что ничего больше настраивать не нужно: ядро single-operator local-first без сетевого auth-гейта, SQLite — `~/.throne/throne.db`, workspace — `~/.throne/workspaces`.

### 3. Самообновление

```bash
throne update              # latest из GitHub Releases → atomic swap install-каталога
throne update --force      # обновиться даже если версия совпала
throne update --restart    # после подмены перезапустить бинарь
```

### 4. Static operational skills для dogfooding

Operational layer лежит в репозитории обычными файлами:

- `skills/intent/SKILL.md` + `skills/intent/bin/throne-intent`
- `skills/review/SKILL.md` + `skills/review/bin/throne-review`
- `skills/dream/SKILL.md` + `skills/dream/bin/throne-dream`

Embedded Run сам прикладывает нужные skills и инжектит `THRONE_INTENT_ID`, `THRONE_API_BASE`, а для review — `THRONE_REPOSITORY_BINDING_ID`. Если запускаешь обычную агент-сессию прямо в этом репозитории для dogfooding, приложи нужные `skills/<id>/SKILL.md` вручную и задай:

```bash
export THRONE_API_BASE=http://localhost:5008
export THRONE_INTENT_ID=<intent-id>
```

Для review можно передать binding явно:

```bash
export THRONE_REPOSITORY_BINDING_ID=<binding-id>
```

### Host-фичи: репозитории, агент-терминал, VS Code

`throne` — обычный хостовый процесс: он наследует твой PATH, спавнит `code`/`claude`/`gh`/`git`/`tmux` напрямую, ходит в OS keychain через сами CLI (без plaintext-выгрузки токенов) и использует реальный `ssh-agent`. Поэтому host-фичи **включаются автоматически** по live capability-probe: фича загорается, как только соответствующий CLI оказывается в PATH. Отдельного «host-backend режима» и тоглов в `/settings` больше нет — страница показывает только «Готовность» (что детектится / что доустановить).

- **Репозитории и PR-комменты.** Поставь GitHub CLI (`brew install gh` / [install_linux.md](https://github.com/cli/cli/blob/trunk/docs/install_linux.md)) и `gh auth login` как обычно — Keychain/secret-store подходит, Throne ходит в `gh` сам. SSH-ключи работают через твой `ssh-agent` (запароленные тоже). Клоны ложатся в `~/.throne/workspaces`.
- **Агент-терминал и Run.** Поставь `tmux` (`brew install tmux`) и залогинь Claude Code на хосте (`claude` → `/login`). Кнопка «Запустить агента» поднимает `tmux`-сессию `throne-{intent_id}` с `claude` под твоим аккаунтом и стримит её в браузер; сессия живёт в tmux-демоне и переживает рестарт Throne.
- **Open in VS Code.** Поставь команду `code` в PATH (VS Code → Command Palette → «Shell Command: Install 'code' command in PATH») — capability `vscode` загорится.

Если нужного CLI нет в PATH, соответствующая секция («Репозитории» / «PR comments» / «Терминал») просто не появляется, остальное работает. См. [ADR-0048](specs/ADR/0048-single-binary-packaging.md).

## Структура

```
throne/
├── apps/
│   ├── api/                 # .NET 10 backend (HTTP API + embedded terminal)
│       ├── src/
│       │   ├── Throne.Domain/
│       │   ├── Throne.Application/
│       │   ├── Throne.Infrastructure/
│       │   └── Throne.Api/
│       └── tests/
│           ├── Throne.Domain.Tests/
│           ├── Throne.Application.Tests/
│           ├── Throne.Infrastructure.Tests/
│           ├── Throne.Api.Tests/
│           └── Throne.Architecture.Tests/
│   └── web/                 # Vite + React + TypeScript frontend
│       └── src/             # FSD 2.0: app/pages/widgets/features/entities/shared
├── specs/
│   ├── ADR/                 # Architecture Decision Records
│   └── AGENTS.local.md
├── skills/                  # static provider-neutral operational skills
├── bin/                     # throne-intent/review/dream CLI scripts
├── scripts/quality/         # verify.sh + sub-scripts
├── .quality/                # quality.config.json
├── ROOT.md                  # общие правила для агентов (канон)
├── AGENTS.md                # Codex/agent entrypoint (стаб → ROOT.md)
├── CLAUDE.md                # Claude entrypoint (стаб → ROOT.md)
└── DESIGN.md                # frontend design system
```

## Архитектура

Clean Architecture в `apps/api`. Зависимости — внутрь:

```
Api → Application → Domain
Infrastructure → Application → Domain
Api → Infrastructure (только в DI)
```

Защита направлений — `Throne.Architecture.Tests` на NetArchTest.

См. [ADR-0001](specs/ADR/0001-foundation-clean-architecture-monorepo.md).

Frontend в `apps/web` строится по FSD 2.0. Структуру и imports защищает Steiger.

См. [ADR-0005](specs/ADR/0005-frontend-foundation-fsd-quality-harness.md).

## Quality gates

```bash
bash scripts/quality/verify.sh                     # backend + frontend
bash scripts/quality/verify.sh --fast              # без security audit
bash scripts/quality/verify.sh --scope backend     # только backend
bash scripts/quality/verify.sh --scope frontend    # только frontend
bash scripts/quality/verify-backend.sh             # backend-only
bash scripts/quality/verify-frontend.sh            # frontend-only
```

Запускается перед каждым коммитом и завершением хода агента.

## Технологии

- .NET 10
- SQLite через EF Core (`~/.throne/throne.db` по умолчанию)
- GitHub CLI `gh` + `git` (опционально — для секций «Репозитории» и «PR comments»; включаются автоматически, когда `gh`/`git` есть в PATH)
- Vite + React + TypeScript
- FSD 2.0 + Steiger
- xUnit + FluentAssertions
- Central Package Management (`Directory.Packages.props`)

## Где что искать

| Документ | Что |
|---|---|
| [specs/ADR/REGISTRY.md](specs/ADR/REGISTRY.md) | Реестр архитектурных решений |
| [specs/AGENTS.local.md](specs/AGENTS.local.md) | Правила для AI-агентов в этом проекте |
| [specs/contracts/AGENTS.md](specs/contracts/AGENTS.md) | HTTP API контракты (OpenAPI source of truth) |
| [specs/contracts/realtime/events.yaml](specs/contracts/realtime/events.yaml) | Realtime server→client события (yaml source of truth) + [ADR-0008](specs/ADR/0008-realtime-contract-first-events.md) |
| [specs/manifest/throne-skills.yaml](specs/manifest/throne-skills.yaml) | System instructions + embedded-композиция по режимам (источник правды) |
| [skills/intent/SKILL.md](skills/intent/SKILL.md) / [skills/review/SKILL.md](skills/review/SKILL.md) / [skills/dream/SKILL.md](skills/dream/SKILL.md) | Static operational skills |
| [specs/ADR/0043-static-operational-skills-and-mcp-removal.md](specs/ADR/0043-static-operational-skills-and-mcp-removal.md) | Operational skills as repo files + MCP removal |
| [DESIGN.md](DESIGN.md) | Дизайн-система фронтенда |
| [ROOT.md](ROOT.md) | Общие правила для агентов (канон) |
| [AGENTS.md](AGENTS.md) | Точка входа для Codex/агентов (стаб → ROOT.md) |
| [CLAUDE.md](CLAUDE.md) | Точка входа для Claude (стаб → ROOT.md) |
