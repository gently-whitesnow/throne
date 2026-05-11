# ADR-0018: Intent link graph (M:N edges, stage 1)

## Status

Accepted

## Context

Throne'у нужна возможность связывать интенты «многий ко многим» — для агента (контекст
и трассировка зависимостей) и пользователя (UI-навигация). Альтернативы:

- **Inline-mention в тексте (`@[id]`)** — даёт ноль метаданных и привязывает связи к
  редакциям текста; нет места под direction/type/rationale.
- **Открытые пользовательские типы связей** — Jira/Anytype-урок: вырождается в шум.
- **Двунаправленные пары рёбер** — удваивает запись, ломает идемпотентность delete.

[ADR-0002](0002-domain-model-and-text-versioning.md) описал линейную историю редакций;
здесь добавляется ортогональная сущность графа, не пересекающаяся с `text_versions`.

## Decision

Stage 1 поставляет минимально-полезное ядро: коллекция, MCP/HTTP/realtime контракты,
расширение `get_intent`. Принципы:

1. **Граф ортогонален тексту.** Создание/удаление ребра не бампит `current_version`
   и не меняет `updated_at` — тот же постфикс, что у `MoveTo`. Edges не записываются
   в text-version-историю.
2. **Одно направленное ребро на отношение.** Зеркальные роли (`blocked_by` для
   `blocks`, `source_of` для `derived_from`) — это incoming-проекция в response, а не
   отдельные документы. Уникальность — `(from_id, to_id, type)`.
3. **Закрытое множество типов.** Stage 1: `relates`, `blocks`, `derived_from`.
   `duplicate_of` зарезервирован для stage 3 (merge-семантика — отдельный интент).
4. **Self-link запрещён** (`link.self_link`). Циклы по `blocks` разрешены —
   валидный сигнал пользователю.
5. **Без `expected_version`** для мутаций рёбер: они не конфликтуют с правками текста.
6. **Owner-isolation.** Все запросы фильтруются по `owner_user_id` через
   `ICurrentUserAccessor`. Ребро может быть прочитано/удалено только если
   обе вершины принадлежат текущему пользователю; orphan-edges отбрасываются в
   проекции.
7. **Каскадное удаление.** При `DeleteIntent` инфраструктурный repository удаляет все
   входящие/исходящие рёбра в той же транзакции — никаких отдельных handler'ов.

### Wire shape

Коллекция `intent_links`:

```
{ _id, from_id, to_id, type, author, rationale?, created_at }
```

Индексы:
- `unique(from_id, to_id, type)` — `from_to_type_unique`
- `from_id` — `from_id`
- `to_id` — `to_id`

`get_intent.links[]` отдаёт outgoing + incoming как единый список с полем
`direction` ∈ `{outgoing, incoming}` и inline-peer-preview (id, status,
sort_key, text_short, tags). Пагинации в `get_intent` нет — для high-degree
случаев агент идёт через `list_intent_links`.

### MCP-tools

| Tool | Errors |
|---|---|
| `link_intent` | `link.self_link` / `link.duplicate` / `link.type_unsupported` / `intent.not_found` |
| `unlink_intent` | идемпотентен (success на missing edge) |
| `list_intent_links` | read-only, `direction`/`type`/`limit`/`cursor` |

### HTTP

`/api/v1/intents/{id}/links` (GET, POST), `/api/v1/intents/{id}/links/{to_id}/{type}`
(DELETE, идемпотентен), под существующим OpenAPI codegen ([ADR-0006](0006-openapi-contract-first-codegen.md)).
DTO`:` `IntentLinkDto`, `IntentLinkPeerDto`, `IntentLinkViewDto`,
`IntentLinksPageDto`, `CreateIntentLinkRequest`. `IntentDetailDto` расширен полем
`links`. Все эндпоинты ассимилируют `ApiException` → `ProblemDetails`.

### Realtime

`intent.link_added` (payload — `IntentLinkDto`) и `intent.link_removed`
(payload — `{id, from_id, to_id, type}`). Repository outcomes реализуют
`IDomainEventCarrier`; стандартный `DomainEventDispatchingUnitOfWork`-pipeline
([ADR-0008](0008-realtime-contract-first-events.md)).

## Out of scope (stage 1)

- **Миграция `text_versions` → `intent_events`** — отдельный интент. Аргумент: данные-миграции
  и event-collection touchают весь стек чтения/записи и не зависят от link-сущностей.
  Объединить с stage 2 (UI sidebar + объединённый timeline) выгоднее, чем смешивать
  с link-инфраструктурой.
- **UI-панель связей** — stage 2 (см. описание интента 4bf16bb…).
- **`duplicate_of` со схлопыванием** — stage 3.
- **Внешние сущности (Jira/GitHub/URL)** — `to_kind`/`to_id` остаются дверью на будущее,
  но в этом ADR схема принимает только `intent → intent`.
- **Inline `@[X]` парсер** — read-time view, без хранения рёбер. Может появиться
  отдельным ADR.
- **Глобальный граф-вью** (Roam/Obsidian) — сознательно не делаем.

## Consequences

### Positive

- Агент видит граф через `get_intent.links[]` без отдельного round-trip — правило
  «мощные tools на мутациях, дешёвые проекции на чтении».
- Realtime-события + `useRealtimeEvent` гарантируют, что UI stage 2 встанет на тот же
  контракт без переделки backend'а.
- Каскадное удаление и owner-isolation by construction — нет ghost-edges и нет
  cross-tenant-утечек.
- Quality gate `realtime` проверяет покрытие yaml ↔ events.cs ↔ realtime handler ↔
  frontend subscription.

### Negative / Risks

- Без `expected_version` две одновременные мутации могут создать обе ветки одного
  направления (например, разные `relates` сразу). Это допустимо: уникальность
  `(from, to, type)` исключает дубликаты, а порядок появления видим в timeline.
- `get_intent.links[]` без пагинации — open-ended; high-degree intents потребуют
  либо `list_intent_links`, либо в stage 2 add-on в виде «N+show more».
- Cascade-delete стирает входящие/исходящие edges одной командой; user revert
  возможен только до закрытия транзакции — для stage 1 это OK, дальше можно навесить
  soft-delete если потребуется.
