# Throne

Облако рабочих единиц пользователя (`Intent`, `Instruction`) с MCP-интерфейсом.

## Миссия

Throne — второй мозг пользователя. Хранит его намерения и предпочтения, выдаёт их любому агенту по запросу и учится на каждом диалоге, чтобы агенты достигали максимально ожидаемого результата. Помогает держать и переключать контекст между несколькими задачами.

## Контекст

- **Кто пользователь:** человек-оператор, который использует сильные стороны человека (вкус, воля, семантическое понимание, persistent memory) и сильные стороны AI (пропускная способность, неутомимость, механическая согласованность) в связке.
- **Аудитория:** инфраструктура для класса ai-разработчиков, не персональный инструмент одного автора.
- **Первичный артефакт:** предпочтения пользователя. Решения и командные артефакты — в будущем.
- **Намерения (Intent):** единица намерения, в которую оператор приносит работу — руками или автоматически из jira, мессенджеров, багтрекеров. Throne помогает раскрыть намерение и предоставить его агенту вместе с предпочтениями.
- **Граница:** Throne не заменяет IDE и агентов. Это слой памяти и постановки, к которому подключаются любые агенты в любых средах.
- **Цикл улучшения:** оператор просит агента проанализировать локальные диалоги → отправляет патчи в Throne → пользователь применяет патчи → следующий результат ближе к ожиданиям, чем предыдущий.
- **Цель:** усилить оператора. Оцифровка оператора — побочный эффект того, что система помнит его предпочтения и решения.

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

## Запуск

### Docker Compose

Только MongoDB (replica set `rs0`, порт `27017`):

```bash
docker compose --profile db up -d
```

База + API (`http://localhost:5008`) + веб (`http://localhost:8080`):

```bash
docker compose --profile full up --build
```

Локально без Docker — см. ниже.

```bash
cd apps/api
dotnet restore
dotnet build
dotnet test
```

```bash
cd apps/web
pnpm install
pnpm dev
pnpm build
```

### STDIO-прокси для MCP-клиентов

`Throne.Mcp.Stdio` распространяется как [global .NET tool](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools) в NuGet (`PackageId=Throne.Mcp.Stdio`, команда `throne-mcp-stdio`). Это единственный поддерживаемый способ подключения внешних MCP-клиентов (Claude Desktop/Code, Cursor, Codex) — пути до локального чекаута и пред-собранные бинари в Releases намеренно не используются (см. [ADR-0009 § Distribution](specs/ADR/0009-cross-process-realtime-fanout.md#distribution)).

```bash
dotnet tool install -g Throne.Mcp.Stdio
# обновление
dotnet tool update -g Throne.Mcp.Stdio
```

Публикация — workflow [.github/workflows/publish-mcp-stdio.yml](.github/workflows/publish-mcp-stdio.yml) по тегу `v*`.

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
