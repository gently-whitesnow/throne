# ADR-0019: `intent_events` as unified Intent history

## Status

Accepted

## Context

[ADR-0002](0002-domain-model-and-text-versioning.md) put text edits into the linear
`text_versions` collection (delta-only after v1) with a discriminator covering both
Intent и Instruction. [ADR-0018](0018-intent-link-graph.md) added the orthogonal
`intent_links` graph (stage 1) and explicitly punted а unified history to stage 2.

UI этап 2 требует объединённого `IntentActivityTimeline`, который рендерит и
текстовые правки, и события графа. Рассмотренные альтернативы:

1. **Read-time merge** (Application слой объединяет `text_versions` и `intent_links`
   per request). Дешёво, но скрепляет UI с двумя источниками данных, плодит
   pagination/sorting логику в Application, и не открывает дверь к будущим типам
   событий (`status_changed`, `tags_changed`, …).
2. **Расширить `text_versions`** новым `kind = link_added` / `kind = link_removed`.
   Ломает инвариант линейной версионной шкалы (`version` колонка теряет смысл для
   link-событий), и схема становится дискриминатором двух разных доменов.
3. **Новая коллекция `intent_events`**, единый источник правды для всех событий
   именно intent-агрегата (текст + граф + позже статус/теги). Stage 2 покрывает
   `text_changed` / `link_added` / `link_removed`; остальные kinds — отдельные
   заходы поверх той же схемы.

Cut-over vs dual-write для миграции: dual-write оставляет тот же риск, что и
параллельные истории (drift), и удваивает запись на каждое редактирование.
Cut-over риск — один deploy; rollback — restore Mongo dump (поставка self-hosted,
все базы у пользователя). Cut-over дешевле и проще удерживать в голове.

Instruction text-history остаётся в `text_versions`: `Instruction` не участвует
в графе и в этом этапе UI не получает unified timeline.

## Decision

Stage 2 поставляет `intent_events` как единый append-only лог событий
intent-агрегата. Принципы:

1. **Один документ на событие.** Schema (Mongo collection `intent_events`):
   ```text
   _id            : string  (ObjectId hex)
   intent_id      : string  (primary subject; для link* — from_id)
   peer_intent_id : string? (только для link_added / link_removed = to_id;
                             позволяет отдать unified feed для обоих концов
                             ребра одной выборкой OR-фильтром)
   kind           : string  enum { text_changed | link_added | link_removed }
   version        : int?    (для text_changed — Intent.current_version
                             на момент события; для link* — null)
   text_change    : object? (для text_changed: { kind, snapshot?, old_text?,
                              new_text?, after_line?, insert_text? })
   link           : object? (для link*: { id, from_id, to_id, type, author,
                              rationale?, created_at })
   created_at     : timestamp
   created_by     : string? ("user" | "agent" | "system")
   ```
   `version` сохраняется именно как поле верхнего уровня (а не внутри
   `text_change`), чтобы реплеить full text-версию `text_changed`-документами без
   глубокой проекции.

2. **`text_changed` сохраняет delta-формат ADR-0002.** Первое событие интента —
   `kind=text_changed` с `text_change.kind=create` и `snapshot`; последующие — delta
   `replace` / `insert`. Восстановление полного текста на версию N — replay,
   как и было.

3. **Cut-over миграция.** Hosted service `MongoIntentEventsMigration` стартует
   один раз и копирует все документы `text_versions` где `owner_kind=intent` в
   `intent_events` как `text_changed`-события. Идемпотентна:
   skip если для `(intent_id, version)` уже есть `text_changed` событие.
   После миграции `intents`-write-пути пишут только в `intent_events`.

4. **`text_versions` остаётся как cold backup** до периода стабильности; новый
   код её не пишет и не читает для intents. `Instruction` продолжает её
   использовать без изменений.

5. **Граф пишется в `intent_events` через `IntentLinkAdded` / `IntentLinkRemoved`
   handlers** (внутри той же транзакции, что вставка/удаление в `intent_links`).
   У link-события `intent_id = from_id`, `peer_intent_id = to_id`. Таймлайн
   запрашивает `(intent_id = X) OR (peer_intent_id = X)` — оба конца ребра
   видят событие в своём фиде.

6. **`get` — пагинируемый append-only feed.** Новый HTTP endpoint
   `GET /api/v1/intents/{id}/events` возвращает merged timeline, отсортированный
   `created_at` ASC. `listIntentVersions` точечно репоинтится на `intent_events`
   фильтром `kind=text_changed` (для совместимости с уже сделанными internals
   и тестами); UI использует новый `events` endpoint.

7. **`Instruction.text_versions` остаётся.** ADR-0019 не затрагивает Instruction;
   их история остаётся линейной до отдельного решения.

## Out of scope (этап 2)

- `status_changed`, `tags_changed`, `sortkey_changed` события — следующий заход
  поверх той же схемы.
- Полное удаление коллекции `text_versions` — отложено до отдельного
  «cleanup» интента после периода стабильности.
- Cross-tenant миграция / batch-tools — миграция per-database (каждый юзер
  держит свой self-hosted Throne; см. ADR-0012).

## Consequences

### Positive

- UI получает один endpoint и один тип «событие интента», timeline превращается в
  чистый stream-рендер.
- Будущие kinds (`status_changed`, `tags_changed`) встают в ту же коллекцию без
  миграций UI.
- Граф и текст видны на обоих концах ребра без отдельного join'а.

### Negative / Risks

- Один deploy переключает write-путь intents. Rollback = restore Mongo dump
  и откат деплоя; для self-hosted single-instance это приемлемо.
- Замена записи в `text_versions` ломает любой код, который читал эту коллекцию
  напрямую для intents. Architecture-test и integration-tests страхуют от
  регрессии до merge.
