# ADR-0018: Intent link graph (single directed edge + blocking)

## Status

Accepted

**Update 2026-06-21:** amended by [ADR-0043](0043-static-operational-skills-and-mcp-removal.md). The graph model remains; agent-side create/link operations now run through `bin/throne-intent` over HTTP instead of MCP tools.

## Context

Throne'у нужна возможность связывать интенты «многий ко многим» — для агента (контекст
и трассировка зависимостей) и пользователя (UI-навигация). Альтернативы:

- **Inline-mention в тексте (`@[id]`)** — даёт ноль метаданных и привязывает связи к
  редакциям текста; нет места под direction/blocking/rationale.
- **Открытые пользовательские типы связей** — Jira/Anytype-урок: вырождается в шум.
- **Двунаправленные пары рёбер** — удваивает запись, ломает идемпотентность delete.

[ADR-0002](0002-domain-model-and-text-versioning.md) описал линейную историю редакций;
здесь добавляется ортогональная сущность графа, не пересекающаяся с `text_versions`.

## Decision

Граф поставляет одно минимальное ядро: коллекция, MCP/HTTP/realtime контракты,
расширение `get_intent` и UI-проекции. Принципы:

1. **Граф ортогонален тексту.** Создание/удаление ребра не бампит `current_version`
   и не меняет `updated_at` — тот же постфикс, что у `MoveTo`. Edges не записываются
   в text-version-историю.
2. **Одно направленное ребро.** Направление всегда forward: причина/родитель →
   следствие/потомок. Обратные роли — только projection-time чтение incoming edges.
   Уникальность — `(from_id, to_id)`.
3. **`blocking` вместо типов.** `blocking=true` — жёсткая зависимость/actionable
   сигнал «blocked by». `blocking=false` — мягкий контекст/происхождение.
   `relates`, `derived_from`, `blocks`, `duplicate_of` не являются частью новой модели.
4. **Self-link запрещён** (`link.self_link`). Циклы по blocking-edges разрешены —
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

Индексы:
- `unique(from_id, to_id)` — `from_to_unique`
- `from_id` — `from_id`
- `to_id` — `to_id`

Документ:

```
{ _id, from_id, to_id, blocking, author, rationale?, created_at }
```

`get_intent.links[]` отдаёт outgoing + incoming как единый список с полем
`direction` ∈ `{outgoing, incoming}` и inline-peer-preview (id, status,
sort_key, text_short, tags). Пагинации в `get_intent` нет — для high-degree
случаев агент идёт через `list_intent_links`.

### MCP-tools

| Tool | Errors |
|---|---|
| `link_intent` | `link.self_link` / `link.duplicate` / `intent.not_found` |
| `unlink_intent` | идемпотентен (success на missing edge) |
| `list_intent_links` | read-only, `direction`/`blocking`/`limit`/`cursor` |

### HTTP

`/api/v1/intents/{id}/links` (GET, POST), `/api/v1/intents/{id}/links/{to_id}`
(DELETE, идемпотентен), под существующим OpenAPI codegen ([ADR-0006](0006-openapi-contract-first-codegen.md)).
DTO`:` `IntentLinkDto`, `IntentLinkPeerDto`, `IntentLinkViewDto`,
`IntentLinksPageDto`, `CreateIntentLinkRequest`. `IntentDetailDto` расширен полем
`links`. Все эндпоинты ассимилируют `ApiException` → `ProblemDetails`.

### Realtime

`intent.link_added` (payload — `IntentLinkDto`) и `intent.link_removed`
(payload — `{id, from_id, to_id, blocking}`). Repository outcomes реализуют
`IDomainEventCarrier`; стандартный `DomainEventDispatchingUnitOfWork`-pipeline
([ADR-0008](0008-realtime-contract-first-events.md)).

## Out of scope

- **Миграция `text_versions` → `intent_events`** — отдельный интент. Аргумент: данные-миграции
  и event-collection touchают весь стек чтения/записи и не зависят от link-сущностей.
  Объединить с stage 2 (UI sidebar + объединённый timeline) выгоднее, чем смешивать
  с link-инфраструктурой.
- **`duplicate_of` со схлопыванием** — отменено: дубль не отдельный тип ребра.
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

- Без `expected_version` две одновременные мутации могут спорить за одно ребро.
  Уникальность `(from, to)` исключает дубликаты, а порядок появления видим в timeline.
- `get_intent.links[]` без пагинации — open-ended; high-degree intents потребуют
  либо `list_intent_links`, либо в stage 2 add-on в виде «N+show more».
- Cascade-delete стирает входящие/исходящие edges одной командой; user revert
  возможен только до закрытия транзакции — для stage 1 это OK, дальше можно навесить
  soft-delete если потребуется.

## Amendments

### 2026-06-20: схлопывание typed graph

Изначальная модель `relates` / `blocks` / `derived_from` / reserved `duplicate_of`
оказалась шире реального использования. Модель заменена на одну направленную связь
с `blocking`.

Миграция:

- `blocks` сохраняет направление и становится `blocking=true`.
- `derived_from` разворачивается из child→parent в parent→child и становится
  `blocking=false`.
- `relates` и `duplicate_of` удаляются.
- При коллизии после разворота одно `(from_id,to_id)` ребро с `blocking=true`
  побеждает soft-ребро.
