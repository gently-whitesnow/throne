# Throne

Облако рабочих единиц пользователя (`Intent`, `Instruction`) с MCP-интерфейсом и MongoDB как canonical storage.

## Миссия

Throne хранит работу пользователя как поток `Intent`'ов: минимальных формализованных намерений, из которых агент может уточнять задачу, выполнять работу и сохранять следы непонимания.

Главная цель MVP — догфудить сам процесс работы с AI и начать собирать данные для следующей итерации улучшения interview/work. В первой версии система не «обучается» автоматически; она аккуратно сохраняет материал, на котором это можно будет построить позже:

- какие вопросы агент задавал;
- какие ответы дал пользователь;
- как после этого изменился `Intent.text`;
- где агент ошибся во время work;
- какие review-замечания оставил пользователь.

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
| [apps/api/src/Throne.Application/Instructions/EnsureSeedInstructionsHandler.cs](apps/api/src/Throne.Application/Instructions/EnsureSeedInstructionsHandler.cs) | Seed-инструкции Throne |
| [DESIGN.md](DESIGN.md) | Дизайн-система фронтенда |
| [AGENTS.md](AGENTS.md) | Точка входа для Codex/агентов |
| [CLAUDE.md](CLAUDE.md) | Точка входа для Claude |
