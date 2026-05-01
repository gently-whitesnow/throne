# ADR-0001: Foundation — Clean Architecture в монорепо

## Status

Accepted

## Context

`Throne` — solo-first MVP облака `Intent`/`Instruction` с MCP-интерфейсом и MongoDB. Ценность продукта — собирать материал для будущего обучения системы (qa, review, версии). Ядро — доменные модели и инварианты редактирования текста (exact replace, optimistic concurrency, версионирование). Транспорт (MCP) и хранилище (MongoDB) — детали, которые нужно изолировать от ядра, чтобы:

- доменные правила тестировались без MongoDB и без MCP-протокола;
- появление будущего фронтенда (HTTP API) не требовало переписывать домен;
- замена транспорта/хранилища была локализованной.

Альтернативы:

1. **Vertical slice / single-project layout.** Быстрее на старте, но размывает ядро правил версионирования и редактирования текста среди транспорта и хранилища.
2. **Modular monolith с явными модулями.** Преждевременно для MVP с одним bounded context (`Intent` + `Instruction` тесно связаны).
3. **Clean Architecture (выбрано).** Чёткое разделение Domain / Application / Infrastructure / Api. Цена — четыре проекта вместо одного, но они тонкие и не мешают dogfooding.

Рекомендации [quality-harness-template/docs](../../../quality-harness-template/docs) применяются как sidecar (ADR-0001 шаблона: harness — справочный материал, не embedded зависимость): baseline + architecture-clean + maintainability (legacy) + security audit + loop-discipline; coverage/mutation/openapi отложены.

## Decision

1. **Монорепо** с layout `apps/api/` (.NET backend) и будущим `apps/web/` (фронтенд). На корне репозитория — `specs/`, `scripts/quality/`, `.quality/`, `AGENTS.md`, `CLAUDE.md`, `USER.md`.
2. **Clean Architecture** в `apps/api/`:
   - `Throne.Domain` — entities, value objects, domain rules. Без внешних зависимостей.
   - `Throne.Application` — use cases и порты репозиториев. Без знания о MongoDB и MCP.
   - `Throne.Infrastructure` — реализация портов на MongoDB.
   - `Throne.Api` — composition root и transport. Сейчас MCP через `ModelContextProtocol.AspNetCore`; в будущем сюда же сядут HTTP-эндпойнты для `apps/web`. Имя `Api`, а не `Mcp`, осознанно более абстрактно.
3. **Зависимости — внутрь**: `Api → Application → Domain`, `Infrastructure → Application → Domain`, `Api → Infrastructure` только в DI-регистрации. Защита — `Throne.Architecture.Tests` на NetArchTest.Rules.
4. **Tech stack**: .NET 10, MongoDB Driver, official `ModelContextProtocol` C# SDK, Central Package Management (`Directory.Packages.props`).
5. **Тесты**: xUnit + FluentAssertions + Testcontainers для интеграционных Mongo-тестов; NSubstitute для моков портов.
6. **Quality harness packs**, адаптированные из [quality-harness-template](../../../quality-harness-template):
   - baseline (`.editorconfig`, `Directory.Build.props`, скрипты);
   - architecture-clean (NetArchTest правила слоёв);
   - maintainability — профиль `legacy` (advisory), будет добавлен позже одной проходкой baseline;
   - security audit (`dotnet list package --vulnerable`);
   - loop-discipline — единая точка `scripts/quality/verify.sh`.
7. **Отложено**: coverage, mutation, openapi-dotnet, agent-guardrails. Включаем по мере появления реальных tools и use cases.
8. **MVP не реализуется в этой итерации**: MCP tools, доменные модели, версионирование, idempotent seed bootstrap. Это следующий шаг, опирающийся на этот фундамент.

## Consequences

### Positive

- Доменные правила (exact replace, версии, конфликты) можно покрывать unit-тестами без MongoDB.
- Будущий фронтенд (`apps/web`) добавляется без рефакторинга backend: HTTP-эндпойнты в `Throne.Api` рядом с MCP.
- NetArchTest защищает от деградации архитектуры: попытка `using Throne.Infrastructure;` в Domain провалит тесты.
- CPM держит версии пакетов в одном месте — критично для монорепо.
- Quality gates через `verify.sh` дают агенту единую команду «всё ли в порядке».

### Negative / Risks

- Четыре .NET проекта вместо одного — больше boilerplate. Цена принята: на скорости MVP сказывается мало, плата за чистоту высока.
- `ModelContextProtocol` SDK в preview — версия может ломаться. Мы фиксируем версию в `Directory.Packages.props` и обновляем осознанно.
- Maintainability baseline пока не сгенерирован — пройдёт первой реальной проходкой после появления доменного кода.
- Testcontainers требует Docker на машине разработчика и в CI.
