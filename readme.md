# Throne

Облако рабочих единиц пользователя (`Intent`, `Instruction`) с MCP-интерфейсом.

## Миссия

Throne — память и постановка задач для человека, который работает в связке с AI-агентами. Хранит намерения и предпочтения, выдаёт их любому агенту по запросу и учится на каждом диалоге, чтобы результат был ближе к ожиданиям. Помогает держать и переключать контекст между несколькими задачами.

## Контекст

- **Кто пользователь:** человек, который сочетает свои сильные стороны (вкус, воля, семантическое понимание, persistent memory) с сильными сторонами AI (пропускная способность, неутомимость, механическая согласованность).
- **Аудитория:** инфраструктура для класса ai-разработчиков, не персональный инструмент одного автора.
- **Первичный артефакт:** предпочтения человека. Решения и командные артефакты — в будущем.
- **Намерения (Intent):** единица намерения, куда приносится работа — руками или автоматически из jira, мессенджеров, багтрекеров. Throne помогает раскрыть намерение и передать его агенту вместе с предпочтениями.
- **Граница:** Throne не заменяет IDE и агентов. Это слой памяти и постановки, к которому подключаются любые агенты в любых средах.
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

API + UI + Mongo одной командой. UI: `http://localhost:8080`, API: `http://localhost:5008`.

```bash
git clone https://github.com/gently-whitesnow/throne
cd throne
docker compose --profile full up --build -d
```

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

### Опционально: связь с GitHub (репозитории и PR-комменты)

Для базового Throne (MCP-память, интенты, инструкции) ничего больше не нужно. Если хочется привязывать к интентам репозитории, видеть PR-комменты в UI и работать с клонами на хосте — настрой `gh`:

1. Поставь GitHub CLI на хост: `brew install gh` (macOS) / см. [install_linux.md](https://github.com/cli/cli/blob/trunk/docs/install_linux.md).
2. Логин **без системного keyring** — токен должен лежать в `~/.config/gh/hosts.yml`, иначе bind-mount в контейнер пробросит только хост без авторизации, и UI покажет `Requires authentication (HTTP 401)`:
   ```bash
   gh auth logout -h github.com   # если уже логинился раньше
   gh auth login --insecure-storage
   ```
   На macOS дефолт `gh` — Keychain (видно как `Token: gho_***  (keyring)` в `gh auth status`); контейнер в Keychain не ходит, поэтому нужен plaintext в `hosts.yml` (`0700`-папка `~/.config/gh`).
3. Перезапусти compose: контейнер API монтирует `~/.config/gh` (read-only), `~/.ssh` (read-only) и клонирует репозитории в `~/.throne/workspaces` на хосте, откуда их видно из любой IDE/терминала. `~/.ssh` нужен, если в `gh auth login` выбран `git_protocol: ssh` (дефолт) — без него `gh repo clone` упадёт на `cannot run ssh: No such file or directory` / `Permission denied (publickey)`. Если ssh-ключ запаролен — `ssh-agent` контейнером не пробрасывается, заведи ключ без passphrase или переключи `gh` на HTTPS (`gh config set git_protocol https -h github.com`).

Без этого секции «Репозитории» и «PR comments» на странице интента просто будут пустыми; остальная функциональность работает.

### Опционально: встроенный агент-терминал

Кнопка «Запустить агента» на странице интента поднимает в контейнере `tmux`-сессию с `claude` и стримит её в браузер. Тулчейн (`tmux`/`git`/`claude`) — Linux-нативный и живёт в образе API; «хостовость» означает, что он работает поверх **хостового состояния**, которое compose монтирует bind-mount'ами. Чтобы агент авторизовался под твоим аккаунтом:

1. Поставь и залогинь Claude Code на хосте хотя бы раз (`claude` → `/login`) — чтобы появились `~/.claude/` и `~/.claude.json`.
2. Авторизация **без системного keyring** — токен должен лежать в `~/.claude/.credentials.json`, иначе bind-mount пробросит в контейнер состояние без кред, и агент попросит логин внутри терминала. На macOS дефолт — Keychain (контейнер в него не ходит), поэтому выгрузи токен в файл:
   ```bash
   security find-generic-password -s "Claude Code-credentials" -w \
     > ~/.claude/.credentials.json
   chmod 600 ~/.claude/.credentials.json
   ```
3. Перезапусти compose: API монтирует `~/.claude` (rw — claude пишет историю/настройки) и `~/.claude.json`, и запускает агента под твоим аккаунтом в `~/.throne/workspaces` на хосте.

Кнопки «Открыть в VS Code» намеренно нет в контейнерном профиле: `code` — desktop-бинарь хоста, capability `vscode` репортует `detected=false`.

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
- GitHub CLI `gh` + `git` (опционально — для секций «Репозитории» и «PR comments»; auth берётся с хоста через bind-mount `~/.config/gh`)
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
