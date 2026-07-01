# ADR-0008 — Realtime: contract-first server-to-client events via domain events

## Status
Accepted (revised after first iteration: see "Decision history" at the bottom).

## Context

UI на `apps/web` после действий пользователя или агента почти всегда требовал ручного refresh: списки и детали Intent не реагировали на серверные изменения. Хочется системного решения, аналогичного OpenAPI:

- единый источник правды для realtime-событий, от которого зависят backend и frontend;
- автоматические проверки, которые ловят добавление события «наполовину»;
- паттерн, при котором добавление новой write-операции **не может «забыть» опубликовать событие**;
- расширяемый pipeline, в который завтра встанут внешний брокер, денормализованные read-models и audit/history-обработчики.

Scope этого ADR — server-to-client realtime fanout. Пользовательские и агентские write-команды остаются на HTTP / MCP.

## Decision

### 1. Источник правды — `specs/contracts/realtime/events.yaml`

Декларативный список событий: имя, описание, ссылка на payload (по схемам OpenAPI Intents) или inline-схема. Добавление нового realtime-события начинается здесь.

### 2. Codegen

`scripts/quality/codegen-frontend.sh` (через `apps/web/scripts/codegen-realtime.mjs`) из YAML генерит:

- `apps/api/src/Throne.Contracts/Generated/RealtimeEventNames.g.cs` — константы и `All` (namespace остаётся `Throne.Realtime.Contracts.Generated`);
- `apps/web/src/shared/realtime/generated/events.ts` — `RealtimeEventMap`, `RealtimeEventName`, `realtimeEventNames`, `RealtimeEventEnvelope`.

Drift-гейт `contracts` ловит расхождения между YAML и сгенерированными файлами.

### 3. Транспорт

Server-Sent Events на `GET /api/v1/realtime/stream`. Каждый event-frame: `event: <name>\ndata: <json>\n\n`. Один subscription на HTTP-соединение, in-process broker (`InMemoryRealtimeBroker`) с per-subscription bounded channel и `DropOldest` для slow consumers. Keep-alive комментарий каждые 15 секунд. Reconnect — стандартный для `EventSource`.

WebSocket появится отдельным ADR, если понадобится full-duplex. Multi-instance fanout — отдельный ADR (NATS/Redis Streams).

### 4. Доменные события и автоматический dispatch (это сердце паттерна)

Вместо ручного `await realtime.PublishX(...)` после каждого `unitOfWork.ExecuteAsync(...)`, fanout происходит автоматически через декорированный unit of work.

Цепочка:

1. **Domain events** (`Throne.Application.Events`):
   - `IDomainEvent` marker + 8 records (`IntentCreated`, `IntentDeleted`, `IntentTextChanged`, `IntentStatusChanged`, `IntentQaAdded`, `IntentReviewAdded`, `IntentAttachmentAdded`, `IntentAttachmentDeleted`).
   - `IDomainEventCarrier` — интерфейс с `IReadOnlyList<IDomainEvent> Events`.

2. **Outcomes** (`Throne.Application.Ports`):
   - Каждый исход успешной мутации реализует `IDomainEventCarrier` и возвращает соответствующий event(s) на success-ветке.
   - Failure-ветки (`NotFound`, `Conflict`, `MatchAmbiguous`) реализуют тот же интерфейс с пустым списком.
   - Для путей без outcome (раньше CreateIntent / Upload/DeleteAttachment) добавлены тонкие wrapper-исходы (`CreateIntentOutcome`, `UploadIntentAttachmentOutcome`, `DeleteIntentAttachmentOutcome`).

3. **Repository** (`Throne.Infrastructure.Mongo`):
   - Конструирует outcome с правильным набором events. Это единственное место, где events создаются.
   - Например, `MongoIntentRepository.SetStatusAsync` возвращает `SetIntentStatusOutcome.Updated(intent)`, чей `Events` отдаёт `[new IntentStatusChanged(intent)]`.

4. **`IDomainEventDispatcher`** (Application port) + `DomainEventDispatcher` (default impl): фанаутит события на все зарегистрированные `IDomainEventHandler`.

5. **`DomainEventDispatchingUnitOfWork`** — декоратор `IUnitOfWork`. После успешного commit (или non-transactional run) проверяет: если `T` реализует `IDomainEventCarrier`, drain'ит events и зовёт диспетчер. Регистрируется в Infrastructure DI как `IUnitOfWork`, оборачивая `MongoUnitOfWork`.

6. **`IUnitOfWork.ExecuteOutsideTransactionAsync<T>`** — добавлен для операций, которые не могут идти в Mongo-транзакции (GridFS upload / delete), но всё равно должны проходить через диспетчер. Декоратор оборачивает обе ветки.

7. **`RealtimeDomainEventHandler`** (Throne.Api) — единственный sink на сегодня. Switch по типу `IDomainEvent` → `RealtimeEventEnvelope` через `IntentDtoMapper` → broker.

Добавляя завтра внешний брокер или event-store, мы регистрируем ещё один `IDomainEventHandler` — handlers Application/Infrastructure не меняются.

Handlers Application больше не знают про realtime: их подпись не содержит публикатор, тело — просто `await unitOfWork.ExecuteAsync(...)` с возвращаемым outcome.

### 5. Frontend client

`apps/web/src/shared/realtime/`:
- `realtimeClient` — синглтон-обёртка над `EventSource`, lazy-connect на первого подписчика, `close()` после последнего;
- `useRealtimeEvent(name, handler)` — типобезопасный React-хук; payload типизирован через `RealtimeEventMap[name]`.

### 6. Quality gate `realtime`

`scripts/quality/realtime-verify-coverage.sh` запускается в `verify-backend.sh` после `contracts`. Для каждого имени из YAML проверяет:

1. `record <PascalName>(...)` есть в `Throne.Application/Events/IntentEvents.cs`;
2. `RealtimeEventNames.<PascalName>` упомянут в `RealtimeDomainEventHandler.cs` (т.е. handler знает, как маппить событие);
3. хотя бы один `useRealtimeEvent("<name>"` есть под `apps/web/src/`.

Дополнительно `openapi-verify-generated-clean.sh` ловит drift в сгенерированных файлах.

«Полусобранное» событие невозможно: YAML, domain-event record, handler-case и frontend-подписчик должны двигаться вместе. Если разработчик добавил новую write-операцию, но забыл создать outcome с events — это не ловится тестом, но сразу видно по review (handler возвращает что-то, что не реализует `IDomainEventCarrier`). На уровне самой схемы это можно усилить через анализатор; пока считаем достаточным.

### 7. Первый rollout (intents vertical slice)

Покрыты: `intent.created`, `intent.deleted`, `intent.text_changed`, `intent.status_changed`, `intent.qa_added`, `intent.review_added`, `intent.attachment_added`, `intent.attachment_deleted`. Подписчики:
- `IntentBoard` — `created/deleted/text_changed/status_changed`;
- `IntentDetailPage` — рефрешит intent на `text_changed/status_changed`, обновляет timeline на `qa_added/review_added`, навигирует прочь на `intent.deleted` для текущего id;
- `IntentAttachmentsPanel` — рефрешит список на `attachment_added/deleted`.

## Consequences

Положительные:
- handlers больше не публикуют realtime сами — публикация автоматическая;
- pipeline переиспользуем: завтра тот же `IDomainEventHandler` поведёт события в внешний брокер, в события-сорсинг, в обновление денормализованных read-моделей;
- история изменений (`text_versions`, `intent_status_changes`) — главные кандидаты на следующий handler-shift; пока остаются inline в repo для атомарности с Mongo write, но их можно постепенно перевести в дополнительные handlers по мере расширения паттерна;
- gate `realtime` падает, пока YAML, Application.Events record, RealtimeDomainEventHandler case и `useRealtimeEvent` не сходятся в одном PR.

Компромиссы:
- broker in-memory; multi-instance потребует внешнего pub/sub — отдельное ADR;
- outcome-shape now carries domain events: репозиторий должен помнить положить event в success-ветку. Анализатор/архитектурный тест может в будущем ловить, что write-метод репозитория возвращает `IDomainEventCarrier`-implementing type;
- non-transactional путь требует `ExecuteOutsideTransactionAsync` (для GridFS), это вторая публичная точка UoW. Конкретная реализация в MongoUnitOfWork — простой проброс работы без сессии.

## Alternatives considered

- **Domain events на самой Entity (Intent.PendingEvents).** Чище в DDD, но требует, чтобы все мутации шли через load-mutate-save в репо; сейчас Mongo-репо делает atomic update без in-memory мутации. Откладываем до следующей итерации.
- **AsyncAPI как формат.** Стандарт интересный, но генераторы под C#/TS неровные; мы не используем channel/operation концепции выше нашего scope. Маленький YAML локально проще.
- **Per-handler IRealtimeEventPublisher с Publish<Name>Async (первая итерация ADR-0008).** Читалось хорошо, но дублировалось в каждом handler и легко забывалось при добавлении новой операции. Заменено на текущий паттерн с `IDomainEventCarrier` + UoW-декоратором.
- **WebSocket / SignalR.** Избыточно для server→client one-way; SSE дешевле и проще.

## References

- [specs/contracts/realtime/events.yaml](../contracts/realtime/events.yaml)
- [scripts/quality/realtime-verify-coverage.sh](../../scripts/quality/realtime-verify-coverage.sh)
- [Throne.Application/Events/](../../apps/api/src/Throne.Application/Events/)
- [Throne.Application/Events/DomainEventDispatchingUnitOfWork.cs](../../apps/api/src/Throne.Application/Events/DomainEventDispatchingUnitOfWork.cs)
- [Throne.Api/Realtime/RealtimeDomainEventHandler.cs](../../apps/api/src/Throne.Api/Realtime/RealtimeDomainEventHandler.cs)
- [Throne.Api/Realtime/RealtimeController.cs](../../apps/api/src/Throne.Api/Realtime/RealtimeController.cs)
- [apps/web/src/shared/realtime/](../../apps/web/src/shared/realtime/)

## Decision history

- v1 (initial): `IRealtimeEventPublisher` с одним методом на каждое событие; handlers явно вызывают `Publish<Name>Async`. Гейт ловил несоответствия, но не «забытый publish-вызов» в новом handler.
- v2 (this revision): outcomes carry events; UoW decorator dispatches; handlers free of realtime knowledge. Pipeline pluggable for future history/broker handlers.
