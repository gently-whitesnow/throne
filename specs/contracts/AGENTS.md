# specs/contracts — правила для агентов

Здесь живёт **источник правды** для HTTP API Throne. Один OpenAPI документ →
- C# DTO + abstract AspNetCore controller (через NSwag);
- TypeScript типы + endpoint table (через `openapi-typescript`).

Никаких ручных DTO, route literals (`app.MapGet("/api/v1/...")`) или дублей доменных моделей в API surface. Если контракта нет — нет HTTP-метода.

## Раскладка

```
specs/contracts/
  shared.yaml              # ProblemDetails и общие типы (RFC 7807)
  <module>/
    openapi.yaml           # paths + components.schemas одного модуля
```

Имя `<module>` ↔ префикс пути и имена .NET сборок:
- `paths` начинаются с `/api/v1/<module>` (множественное число);
- C# DTO живут в `Throne.<Module>.Contracts.Generated`;
- abstract controller в `Throne.Api.Generated`.

## Как добавить метод в существующий модуль

1. Отредактировать `specs/contracts/<module>/openapi.yaml` — добавить путь/operationId/схемы.
2. `bash scripts/quality/openapi-generate.sh apps/api/nswag.<module>.json` — регенерация .NET.
3. `bash scripts/quality/codegen-frontend.sh` — регенерация TypeScript.
4. Реализовать abstract метод в ручном `apps/api/src/Throne.Api/<Module>/<Module>Controller.cs : <Module>ControllerBase`.
5. Добавить handler в `Throne.Application` (+ порт/реализацию в `Throne.Infrastructure`, если нужно новое чтение/запись).
6. `bash scripts/quality/verify.sh` — gate `contracts` падает на drift и неправильных артефактах.

Коммитим **одним PR**: yaml + сгенерированные `.g.cs` / `.ts` + ручной controller + handler.

## Как добавить новый модуль

1. Создать `specs/contracts/<module>/openapi.yaml`.
2. Создать проект `apps/api/src/Throne.<Module>.Contracts/Throne.<Module>.Contracts.csproj` (только `Generated/*.g.cs`).
3. Создать NSwag config `apps/api/nswag.<module>.json` (клон существующего, заменить переменные).
4. Расширить `scripts/quality/openapi-verify-generated-clean.sh` или его caller — добавить путь в проверяемый список (`generated_paths`).
5. ADR обязателен (новые границы и сборки — модульные решения).

## Что нельзя делать

- Редактировать сгенерированные `*.g.cs` или `apps/web/src/shared/api/generated/**` руками.
- Дописывать `app.MapGet/Post/...` для путей, которые принадлежат OpenAPI контракту.
- Возвращать из API доменный объект напрямую — controller всегда маппит в DTO из `Generated`.
- Прятать поля DTO под `text` для list-эндпоинтов (риск выгрузки больших тел). Используйте `text_short` или отдельный read-endpoint.

## Соглашения именования

- `operationId`: `camelCase`, глагол + сущность (`listIntents`, `getIntent`, `createIntent`).
- Схемы DTO: `PascalCase` + суффикс роли (`IntentListItemDto`, `CreateIntentRequest`, `IntentDetailDto`).
- JSON-поля в DTO: `snake_case` (соответствует существующим .NET options и MCP-контрактам Throne).
- Ошибки: `application/problem+json` со схемой `ProblemDetails` из `shared.yaml`.
