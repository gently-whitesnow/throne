# AGENTS.local — Throne project specifics

Проектные правила для агентов. Системные инструкции Throne живут как in-code [SystemInstructionCatalog](../apps/api/src/Throne.Application/Instructions/SystemInstructionCatalog.cs) (scope=`system`, версионируется с релизом). Per-project tech-stack правила и пользовательские предпочтения хранятся как `scope=user` записи в Mongo collection `instructions` для `user_id="mvp-user"` — бутстрапятся mongosh-скриптом [scripts/seed/seed-mvp-user-instructions.js](../scripts/seed/seed-mvp-user-instructions.js). Пустые user-антагонисты (`interview`/`dream`/`fix`) допустимы и редактируются пользователем напрямую через mongosh, у агента write-surface для Instruction нет (см. ADR-0003).

## Перед завершением хода

```bash
bash scripts/quality/verify.sh
```

Должно вернуть PASS. Чинить root cause, не обходить гейты.

## Архитектурные слои (apps/api)

Зависимости — строго внутрь:

```
Api ──► Application ──► Domain
Infrastructure ──► Application ──► Domain
Api ──► Infrastructure (только в Program.cs / DI wiring)
```

- **Throne.Domain** — entities, value objects, доменные правила. Без внешних зависимостей.
- **Throne.Application** — use cases и порты (`IIntentRepository`, `IInstructionRepository`). Не знает про MongoDB и MCP.
- **Throne.Infrastructure** — реализация портов (Mongo).
- **Throne.Api** — composition root + транспорт. Сейчас MCP, в будущем HTTP для `apps/web`.

Нарушение направления зависимостей провалит `Throne.Architecture.Tests`.

## Frontend / UI

При работе над `apps/web` или UI-компонентами используй [DESIGN.md](../DESIGN.md) как источник проектной дизайн-системы.

## Realtime события (domain events + auto-dispatch)

Server-to-client события описаны в [specs/contracts/realtime/events.yaml](contracts/realtime/events.yaml). Транспорт — SSE на `GET /api/v1/realtime/stream`. См. [ADR-0008](ADR/0008-realtime-contract-first-events.md).

**Handlers Application НЕ публикуют realtime сами.** Repository outcome реализует `IDomainEventCarrier`; декоратор `DomainEventDispatchingUnitOfWork` после `unitOfWork.ExecuteAsync(...)` автоматически фанаутит events через `IDomainEventDispatcher` → `RealtimeDomainEventHandler` → SSE-broker.

Добавление нового realtime-события (gate `realtime` падает при «половинной» интеграции):

1. Расширь [events.yaml](contracts/realtime/events.yaml): имя, описание, `payload` или `payload_ref`.
2. Регенерация: `bash scripts/quality/codegen-frontend.sh` обновит `Throne.Realtime.Contracts/Generated` и `apps/web/src/shared/realtime/generated`.
3. Добавь record в [Throne.Application/Events/IntentEvents.cs](../apps/api/src/Throne.Application/Events/IntentEvents.cs) (имя — PascalCase от `<event.name>`, например `intent.text_changed` → `IntentTextChanged`).
4. Сделай так, чтобы соответствующий **outcome** (или новый wrapper-outcome) возвращал этот event на success-ветке через `Events`.
5. Mongo-репо положит event в outcome — никаких publish-вызовов писать не нужно.
6. Добавь case в [RealtimeDomainEventHandler.cs](../apps/api/src/Throne.Api/Realtime/RealtimeDomainEventHandler.cs), маппя domain event → `RealtimeEventNames.<PascalName>` + DTO.
7. Подпишись через `useRealtimeEvent("<name>", handler)` хотя бы в одном месте `apps/web/src/`.

Для не-транзакционных операций (GridFS upload/delete) используй `unitOfWork.ExecuteOutsideTransactionAsync(...)` — декоратор работает и для неё.

Будущие подписчики на тот же поток (внешний брокер, история, denormalized read-models) подключаются как ещё один `IDomainEventHandler` в DI — handlers Application не меняются.

## Изменения, требующие ADR

- Смена архитектурного стиля или layout слоёв.
- Замена storage / транспорта.
- Включение нового quality pack (coverage, mutation, и т.п.).

Шаблон ADR: [specs/ADR/.template.md](ADR/.template.md). После добавления — обнови [specs/ADR/REGISTRY.md](ADR/REGISTRY.md).

## Постановка задачи

Продуктовая постановка приходит вместе с запросом пользователя (например, как приложенный документ или текст в сообщении). В репозитории её не хранится. Не реконструируй намерение из остатков прошлых итераций в коде — спроси, если запрос неполный.
