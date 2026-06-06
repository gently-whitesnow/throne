# Throne

Кокпит цикла разработки вокруг намерения (`Intent`) для человека в связке с AI-агентами. MCP-интерфейс + веб-UI.

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

Throne работает локально. Агент общается с ним через тонкий STDIO-прокси — без облака и без авторизации. Три шага до первого Intent.

### 1. Поставить STDIO-прокси

`Throne.Mcp.Stdio` — это [global .NET tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools) в NuGet (`PackageId=Throne.Mcp.Stdio`, команда `throne-mcp-stdio`), нужен .NET 10 SDK. Это единственный поддерживаемый способ подключения внешних MCP-клиентов (Claude Desktop/Code, Cursor, Codex) — пути до локального чекаута и пред-собранные бинари в Releases намеренно не используются (см. [ADR-0009 § Distribution](specs/ADR/0009-cross-process-realtime-fanout.md#distribution)).

**macOS / Linux**

```bash
dotnet tool install -g Throne.Mcp.Stdio

# GUI-приложения (Claude.app, Cursor) не подхватывают ~/.dotnet/tools.
# Симлинк в системный PATH делает throne-mcp-stdio видимым везде:
sudo ln -sf "$HOME/.dotnet/tools/throne-mcp-stdio" /usr/local/bin/throne-mcp-stdio
```

**Windows**

```bat
dotnet tool install -g Throne.Mcp.Stdio

REM %USERPROFILE%\.dotnet\tools уже в PATH.
REM Открой новое окно терминала/IDE, чтобы PATH перечитался.
```

Обновление — `dotnet tool update -g Throne.Mcp.Stdio`. Публикация в NuGet — workflow [.github/workflows/publish-mcp-stdio.yml](.github/workflows/publish-mcp-stdio.yml) по тегу `v*`.

### 2. Поднять Throne локально

У Throne два режима запуска (см. [ADR-0027](specs/ADR/0027-runtime-model-native-host-process.md)). Базовая работа (MCP-память, интенты, инструкции) одинакова в обоих; различие — где живёт backend и доступны ли host-фичи (терминал, Run, «Open in VS Code», репозитории).

UI: `http://localhost:8080`, API: `http://localhost:5008`.

```bash
git clone https://github.com/gently-whitesnow/throne
cd throne
```

**Контейнерный режим (дефолт).** Весь стек в контейнере, host-фичи выключены (им нужен доступ к хосту, которого у контейнера нет).

```bash
docker compose --profile full up --build -d
```

**Host-backend режим (продвинутый).** Контейнер поднимает только web + Mongo, а API запускается нативно на хосте — тогда он наследует хостовый PATH, спавнит `code`/`claude`/`gh`/`git`/`tmux` напрямую, ходит в OS keychain через сами CLI (без plaintext-экспорта токенов) и использует реальный `ssh-agent`. Host-фичи в Settings загораются. Две независимые команды:

```bash
# 1. web + mongo в контейнере
docker compose -f docker-compose.host.yml up --build -d

# 2. нативный API на хосте (нужен .NET 10 SDK)
#    0.0.0.0 обязателен — web-контейнер ходит в API через host.docker.internal.
ASPNETCORE_URLS=http://0.0.0.0:5008 dotnet run --project apps/api/src/Throne.Api
```

Дефолты host-режима подобраны так, что ничего больше настраивать не нужно: ядро single-operator local-first без сетевого auth-гейта, Mongo — `localhost:27017`, workspace — `~/.throne/workspaces`.

Только MongoDB (replica set `rs0`, порт `27017`):

```bash
docker compose --profile db up -d
```

### 3. Прописать сервер в агенте

Команда везде одна — `throne-mcp-stdio`. Откройте свой клиент и вставьте сниппет в указанный конфиг.

**Claude Desktop** — `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS) · `%APPDATA%\Claude\claude_desktop_config.json` (Windows)

```json
{
  "mcpServers": {
    "throne": {
      "command": "throne-mcp-stdio"
    }
  }
}
```

**Claude Code** — через CLI: `claude mcp add throne -s user -- throne-mcp-stdio` · вручную: `~/.claude.json` (`mcpServers`)

```json
{
  "mcpServers": {
    "throne": {
      "type": "stdio",
      "command": "throne-mcp-stdio"
    }
  }
}
```

**Cursor** — `~/.cursor/mcp.json` (macOS/Linux) · `%USERPROFILE%\.cursor\mcp.json` (Windows)

```json
{
  "mcpServers": {
    "throne": {
      "command": "throne-mcp-stdio"
    }
  }
}
```

**Codex** — `~/.codex/config.toml` (macOS/Linux) · `%USERPROFILE%\.codex\config.toml` (Windows)

```toml
[mcp_servers.throne]
command = "throne-mcp-stdio"
```

### Host-фичи: репозитории, агент-терминал, VS Code

Терминал агента, кнопка Run, «Open in VS Code» и привязка репозиториев требуют доступа к хосту, поэтому работают только в **host-backend режиме** (нативный API на хосте, см. шаг 2 и [ADR-0027](specs/ADR/0027-runtime-model-native-host-process.md)). В контейнерном режиме эти capability недетектятся и остаются выключены — секции «Репозитории» / «PR comments» / «Терминал» просто не появляются, остальное работает.

В host-режиме настройка тривиальна, потому что API — обычный хостовый процесс и пользуется CLI напрямую, без plaintext-выгрузки токенов:

- **Репозитории и PR-комменты.** Поставь GitHub CLI (`brew install gh` / [install_linux.md](https://github.com/cli/cli/blob/trunk/docs/install_linux.md)) и `gh auth login` как обычно — Keychain/secret-store подходит, нативный API ходит в `gh` сам. SSH-ключи работают через твой `ssh-agent` (запароленные тоже). Клоны ложатся в `~/.throne/workspaces`.
- **Агент-терминал и Run.** Поставь `tmux` (`brew install tmux`) и залогинь Claude Code на хосте (`claude` → `/login`). Кнопка «Запустить агента» поднимает `tmux`-сессию `throne-{intent_id}` с `claude` под твоим аккаунтом и стримит её в браузер; сессия живёт в tmux-демоне и переживает рестарт Throne.
- **Open in VS Code.** Поставь команду `code` в PATH (VS Code → Command Palette → «Shell Command: Install 'code' command in PATH»); capability `vscode` загорится.

Включаются фичи тоглами в `/settings` → «Возможности» (default OFF — explicit opt-in после установки соответствующего CLI).

## Структура

```
throne/
├── apps/
│   ├── api/                 # .NET 10 backend (MCP + future HTTP for web)
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
├── scripts/quality/         # verify.sh + sub-scripts
├── .quality/                # quality.config.json
├── AGENTS.md                # Codex/agent entrypoint
├── CLAUDE.md                # Claude entrypoint
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
- MongoDB (replica set обязателен — write-tools используют multi-document transactions; локально: `mongod --replSet rs0` + `rs.initiate()` или docker-compose с `--replSet rs0`, в connection string добавить `?replicaSet=rs0&directConnection=true`)
- GitHub CLI `gh` + `git` (опционально — для секций «Репозитории» и «PR comments»; нативный хостовый `gh auth`, см. host-backend режим)
- Vite + React + TypeScript
- FSD 2.0 + Steiger
- [ModelContextProtocol](https://github.com/modelcontextprotocol/csharp-sdk) (official C# SDK)
- xUnit + FluentAssertions + Testcontainers
- Central Package Management (`Directory.Packages.props`)

## Где что искать

| Документ | Что |
|---|---|
| [specs/ADR/REGISTRY.md](specs/ADR/REGISTRY.md) | Реестр архитектурных решений |
| [specs/AGENTS.local.md](specs/AGENTS.local.md) | Правила для AI-агентов в этом проекте |
| [specs/manifest/throne-skills.yaml](specs/manifest/throne-skills.yaml) | System instructions + bundle манифест (источник правды) |
| [DESIGN.md](DESIGN.md) | Дизайн-система фронтенда |
| [AGENTS.md](AGENTS.md) | Точка входа для Codex/агентов |
| [CLAUDE.md](CLAUDE.md) | Точка входа для Claude |
