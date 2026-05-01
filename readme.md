# Throne

Облако рабочих единиц пользователя (`Intent`, `Instruction`) с MCP-интерфейсом и MongoDB как canonical storage.

## Структура

```
throne/
├── apps/
│   └── api/                 # .NET 10 backend (MCP + future HTTP for web)
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
├── specs/
│   ├── ADR/                 # Architecture Decision Records
│   └── AGENTS.local.md
├── scripts/quality/         # verify.sh + sub-scripts
├── .quality/                # quality.config.json
└── AGENTS.md                # universal coding conventions
```

`apps/web` появится в следующей итерации.

## Архитектура

Clean Architecture в `apps/api`. Зависимости — внутрь:

```
Api → Application → Domain
Infrastructure → Application → Domain
Api → Infrastructure (только в DI)
```

Защита направлений — `Throne.Architecture.Tests` на NetArchTest.

См. [ADR-0001](specs/ADR/0001-foundation-clean-architecture-monorepo.md).

## Запуск

```bash
cd apps/api
dotnet restore
dotnet build
dotnet test
```

## Quality gates

```bash
bash scripts/quality/verify.sh          # все гейты
bash scripts/quality/verify.sh --fast   # без security audit
```

Запускается перед каждым коммитом и завершением хода агента.

## Технологии

- .NET 10
- MongoDB
- [ModelContextProtocol](https://github.com/modelcontextprotocol/csharp-sdk) (official C# SDK)
- xUnit + FluentAssertions + Testcontainers
- Central Package Management (`Directory.Packages.props`)

## Где что искать

| Документ | Что |
|---|---|
| [specs/ADR/REGISTRY.md](specs/ADR/REGISTRY.md) | Реестр архитектурных решений |
| [specs/AGENTS.local.md](specs/AGENTS.local.md) | Правила для AI-агентов в этом проекте |
| [AGENTS.md](AGENTS.md) | Универсальные конвенции |
| [CLAUDE.md](CLAUDE.md) | Точка входа для агента |
