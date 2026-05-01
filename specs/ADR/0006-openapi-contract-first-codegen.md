# ADR-0006: OpenAPI contract-first для backend и frontend

## Status

Accepted

## Context

Между `apps/api` и `apps/web` нужен единый источник правды по HTTP API. Без него фронт и бэк дрейфуют, ручной код DTO/типов и роутов появляется в обеих сторонах, изменение одного метода превращается в скоординированный мульти-PR с риском рассинхронизации в проде. Throne — long-running проект с множеством агентов, и им нужна формальная точка расширения API: «куда писать новый метод».

Альтернативы:

1. **Code-first на бэке + ручные типы на фронте.** Минимум инфраструктуры, но контракт живёт в C# атрибутах и неудобен для агентов фронта; типы фронта дрейфуют.
2. **Code-first на бэке + автогенерация TS из bэкенд-эмитированного OpenAPI.** Решает синхронизацию TS, но контракт всё ещё «вырастает» из C#: нельзя планировать API в yaml до реализации, ревью контракта смешано с ревью кода.
3. **Contract-first: один OpenAPI YAML → C# DTO + abstract controller (NSwag) + TS types/endpoints (openapi-typescript).** Контракт обозрим как отдельный артефакт, изменение API — атомарный PR (yaml + регенерация + ручная реализация).

Выбран вариант 3.

## Decision

1. **Источник правды** — `specs/contracts/<module>/openapi.yaml` + `specs/contracts/shared.yaml` (общий `ProblemDetails` и т.п.).
2. **Backend codegen — NSwag.** Из одного yaml на модуль генерируем:
   - `Throne.<Module>.Contracts.Generated.*Dto` в проекте `apps/api/src/Throne.<Module>.Contracts/` (только `Generated/*.g.cs`);
   - `Throne.Api.Generated.<Module>ControllerBase` (abstract `ControllerBase`) в `apps/api/src/Throne.Api/Generated/`.

   Ручной `apps/api/src/Throne.Api/<Module>/<Module>Controller.cs : <Module>ControllerBase` реализует только поведение. Маршрут и HTTP verb приходят из контракта.
3. **Frontend codegen — `openapi-typescript`** (`apps/web/scripts/codegen-contracts.mjs`). На модуль:
   - `apps/web/src/shared/api/generated/<module>/types.ts` (paths/components);
   - `apps/web/src/shared/api/generated/<module>/endpoints.ts` (таблица `operationId → path`).
4. **Запреты.**
   - Никаких `app.MapGet/Post(...)` для путей, которые принадлежат OpenAPI контракту.
   - Никаких ручных DTO в API surface — controller всегда маппит доменный объект в `*.g.cs` DTO.
   - Запрет на ручные правки `*.g.cs` и `apps/web/src/shared/api/generated/**`.
5. **Quality gate `contracts`** в `scripts/quality/verify-backend.sh` — запускает `openapi-generate.sh` (NSwag) + `codegen-frontend.sh` (`pnpm codegen`) и падает на drift. Гейт стоит **до** `format`/`build`/`test`, чтобы регенерация чинилась в первую очередь.
6. **Список tooling.**
   - Local .NET tool manifest `apps/api/.config/dotnet-tools.json` пинит `nswag.consolecore` 14.7.1 на runtime `Net100`.
   - На один модуль — один `apps/api/nswag.<module>.json`.
   - Frontend devDeps: `openapi-typescript`, `yaml`.
7. **Расширения.** Новый модуль = новый `specs/contracts/<module>/openapi.yaml` + новый `Throne.<Module>.Contracts` + новый `nswag.<module>.json` + запись пути в `openapi-verify-generated-clean.sh`. Расширение модуля — редактирование одного yaml + регенерация. Правила фиксируются в `specs/contracts/AGENTS.md`.

## Consequences

- Bus-factor вокруг API падает: новые методы добавляются по описанной в `AGENTS.md` процедуре без знания .NET reflection или NSwag-секретов.
- Drift между фронтом и бэком ловится одним гейтом до того, как код попадёт в master.
- Цена входа: ещё один `*.csproj`, ещё один tool manifest, поверхность generated-файлов в репозитории; каждое изменение API — это yaml + регенерация (нельзя «по-быстрому» добавить метод). Принимается осознанно.
- NSwag CLI как build-time зависимость требует .NET runtime — у репо это уже есть.
