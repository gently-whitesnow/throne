# ADR-0052: Таск-трекер как read-only прикладываемый контекст интента

## Status

Accepted
Date: 2026-07-06
Related: [ADR-0024](0024-intent-repository-binding-and-cli-providers.md), [ADR-0025](0025-domain-aggregate-style-rich-ddd.md), [ADR-0029](0029-local-first-invariant-and-legacy-auth.md), [ADR-0031](0031-repository-and-verification-artifacts.md), [ADR-0032](0032-gitlab-provider.md), [ADR-0045](0045-throne-extension-pattern.md), [ADR-0046](0046-open-wire-keys-for-extension-axes.md)

## Context

Это первый ADR по оси таск-трекеров, и вводит он её задним числом — ось уже жила в коде как 1:1 mirror-модель `CardSyncLink`, но архитектурно не была зафиксирована. Зеркало работало так: карточка внешней доски (Kaiten) авто-создавала mirror-интент, тело карточки зеркалилось в `Intent.text`, локальные правки текста write-through'ились обратно в карточку (LWW + подавление эха), дети карточки reconcile'ились в рёбра интентов, а исчезновение/архив карточки авто-Reject'ил интент или подменял его stub'ом. Держался инвариант «card-linked интент обязан иметь непустой title» (422 на очистку).

Проблема этой модели — инверсия владения: карточка ВЛАДЕЛА идентичностью, телом и жизненным циклом интента. Двусторонний sync стал источником связности и фоновых сюрпризов (внезапный Reject, перезапись текста, эхо-циклы), а сама идея «внешний трекер решает, что такое интент» противоречит продуктовой модели Throne — **человек задаёт намерение, агенты его выполняют**. Прецедент разворота уже есть: git-репозитории сознательно сделаны как read-through binding без зеркалирования тел в интент ([ADR-0024](0024-intent-repository-binding-and-cli-providers.md), [ADR-0031](0031-repository-and-verification-artifacts.md)) — карточка трекера должна встать в ту же дисциплину. ADR фиксирует ось и разворачивает mirror → attach.

## Subdomain classification

Generic, impl-volatile. Внешние таск-трекеры живут за портом + адаптером (ось расширения по [ADR-0045](0045-throne-extension-pattern.md) / [ADR-0046](0046-open-wire-keys-for-extension-axes.md)): координата карточки провайдер-нейтральна `(tracker, board_id, card_id)`, `tracker` — открытый wire-ключ с валидацией по registry, а vendor-специфика (Kaiten) не течёт в core. Карта — [specs/AGENTS.local.md → Subdomain map](../AGENTS.local.md#subdomain-map-volatility-frame).

## Volatility check

Essential, no source of pressure. Разворот модели владения — продуктовое требование (интент перестаёт принадлежать карточке), а не давление harness'а или хвост миграции.

## Decision

### 1. Агрегат `IntentCardAttachment` вместо mirror-линка

Заводим первоклассный агрегат `IntentCardAttachment` (rich-DDD по [ADR-0025](0025-domain-aggregate-style-rich-ddd.md)): координата `(tracker, board_id, card_id)` + **non-authoritative** снапшот `CardSnapshot(title, description, column_title, archived, card_version, fetched_at)` + availability (`available` / `unavailable` / `gone`). Идентичность (`Id`, `IntentId`, `Coordinate`, `CreatedAt`) иммутабельна после создания; мутабельная часть собрана в `IntentCardAttachmentState`, переходы — фабрики `Create` (успешный pull → `available`) / `Restore` (ре-валидация availability против `CardAvailabilityNames.IsKnown`, чтобы порченая строка падала fast) + инстанс-мутаторы `ApplySnapshot` (свежий pull → снапшот + `available`) и `MarkUnavailable` (деградация без потери снапшота). Ключевой инвариант: снапшот **никогда не пишется вверх и не считается правдой об интенте** — это кэш последнего чтения карточки, не источник.

### 2. Хранилище `intent_card_attachments`, 1:N

Отдельная таблица `intent_card_attachments`, unique `(intent_id, tracker, board_id, card_id)`, отношение **1:N** — на один интент вешается много карточек (mirror давал ровно 1:1). Форма параллельна `intent_repository_bindings` из [ADR-0024](0024-intent-repository-binding-and-cli-providers.md): порт `IIntentCardAttachmentStore` (`ListByIntent` / `Get` / `GetByCoordinate` / `Upsert` / `Delete`), записи внутри `IUnitOfWork.ExecuteAsync`.

### 3. API на интенте — attach / detach / list / refresh

Поверхность intent-scoped: **attach** тянет снапшот из провайдера и сохраняет, идемпотентно по координате (повторный attach той же карточки обновляет снапшот, а не плодит дубль); **detach** — единственный способ снять карточку с интента (204, идемпотентно); **list** отдаёт снапшот + availability, availability на чтении не переопрашивается; **refresh** — ручной online-only ре-pull (кнопка «Обновить»): при недоступности отдаёт прошлый снапшот со статусом, а не ошибку.

Attach требует, чтобы карточка читалась **сейчас**, и его отказы типизированы по границе: неизвестный интент → 404, кривая координата → 422, неподдерживаемый трекер → 422, неподключённый трекер → 409, недостижимый трекер → 502, исчезнувшая/запрещённая карточка → 404. Refresh, в отличие от attach, отказов не даёт — он лишь деградирует availability (см. § 4).

### 4. Read-only инварианты

Никакого write-through, outbox, LWW и reconciliation детей — ничего из mirror-машинерии. `archived` — просто поле снапшота, без сайд-эффекта на интент (**нет** авто-Reject, нет stub при исчезновении карточки). Недоступность карточки деградирует availability (`unavailable` при недостижимом/неподключённом трекере, `gone` при 404/403), но снапшот сохраняется, чтобы оператор видел последнее известное содержимое. Инвариант «card-linked интент держит непустой title» **снят**: карточка больше не владеет title'ом.

### 5. Провайдер-нейтральность

Card-fetch переезжает на выживающую поверхность `ITaskTrackerConnectionProvider.GetCardAsync(connection, cardId, ct)` с контрактом «404/403 → null»; `KaitenTaskTrackerProvider` — первый адаптер, vendor-проекция карточки в neutral-`TaskTrackerCard` инкапсулирована в инфраструктуре. Резолв провайдера — по строковому `tracker`-ключу через registry ([ADR-0045](0045-throne-extension-pattern.md)); неизвестный трекер отбивается 422, неподключённый — 409.

### 6. Read-path — board как фасет, не как класс

В mirror-модели карточка **переклассифицировала** интент: приложенный интент уводился из tag/untagged-бакетов в отдельную board-группу. Теперь board — это **фасет** над обычной классификацией: интент с приложенной карточкой остаётся в своих tag/untagged-бакетах, а board-группа лишь дополнительно агрегирует distinct-интенты с attachment'ом на `(tracker, board)`. Карточка добавляет контекст, а не забирает интент себе.

## Consequences

### Positive

- Владение развязано: интент принадлежит человеку, карточка — read-only контекст поверх, без фоновых mirror-сюрпризов (Reject/перезапись/эхо).
- 1:N даёт гибкость — на интент вешается сколько угодно карточек с разных досок/трекеров.
- Форма переиспользует паттерн binding из [ADR-0024](0024-intent-repository-binding-and-cli-providers.md) (координата + снапшот + availability за портом) — ось таск-трекеров встаёт в ту же дисциплину, что git-репозитории, без нового слоя.
- Провайдер за портом ([ADR-0045](0045-throne-extension-pattern.md) / [ADR-0046](0046-open-wire-keys-for-extension-axes.md)): второй трекер — новый адаптер, а не правка core.

### Negative / Risks

- **Снапшот устаревает.** Обновление — только ручной `refresh`, авто-poll'а нет (осознанный trade-off: read-only контекст не стоит фонового трафика к трекеру; при необходимости авто-refresh — отдельный интент).
- **Realtime attach/detach отложены** в дочерний интент. Гейт realtime требует 4-way синхрона (`events.yaml` ↔ domain ↔ mapper ↔ frontend `useRealtimeEvent`, [ADR-0008](0008-realtime-contract-first-events.md)), а фронт-панель attach-UI сама по себе дочерняя — вводить события до потребителя преждевременно. Пока attach/detach обновляют UI обычным refetch'ем.
- **Фронт-панель attach-UI** — отдельный дочерний слайс: этот ADR фиксирует ось и backend-контур, операторский виджет прикладывания карточек строится следующим.

## Migration

Разворот исполнен **одной drop+create миграцией** `CardMirrorToAttachments`: mirror-таблица `task_tracker_card_links` снесена, `intent_card_attachments` заведена. Ledger миграций **append-only** — исторические файлы не сквошим (удаление middle-миграции ломает уже применённые БД, `Database.MigrateAsync` идёт по журналу). Существующие mirror-интенты **не конвертируются**: grandfathering'а нет намеренно — фича таск-трекеров ещё не в релизе, живых данных под перенос нет.
