# AGENTS.local — Throne project specifics

Проектные правила для агентов. Bundle-маппинг `mode → kinds` и тексты system-инструкций (scope=`system`) живут в декларативном манифесте [specs/manifest/throne-skills.yaml](manifest/throne-skills.yaml) — это единственный источник правды для backend runtime и frontend `/instructions` дерева. Runtime-инструкции попадают агенту через MCP `InitializeResult.instructions` (mini-router) и далее `get_instruction_bundle(mode, intent_id?)` — см. [ADR-0014](ADR/0014-mcp-initialize-instructions-routing.md). Локальных skill-launcher файлов в `.claude/skills/` или `.agents/skills/` больше нет.

## Перед завершением хода

Гейты декларированы в [.quality/quality.config.json](../.quality/quality.config.json), бегунок — [scripts/quality/verify.py](../scripts/quality/verify.py). `verify.sh` — тонкая bash-обёртка для совместимости.

```bash
bash scripts/quality/verify.sh --fast   # быстрый цикл (~1 мин, без integration-тестов и audits)
bash scripts/quality/verify.sh          # перед сдачей хода (полный)
bash scripts/quality/verify.sh --list   # перечень гейтов
bash scripts/quality/verify.sh --only backend-format       # один гейт
bash scripts/quality/verify.sh --skip backend-audit        # без конкретного гейта
bash scripts/quality/verify.sh --scope backend|frontend    # одна сторона
```

`--fast` пропускает `slow: true`: `backend-test-integration`, `backend-audit`, `frontend-audit`. Используй `--fast` в цикле, полный `verify.sh` — перед сдачей хода. Падает — чинить root cause, не обходить.

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
- **Throne.Mcp.Stdio** — тонкий STDIO→HTTP MCP proxy ([ADR-0009](ADR/0009-cross-process-realtime-fanout.md)). Не должен зависеть от Domain/Application/Infrastructure/Api: иначе domain events срабатывают в proxy и SSE-подписчики apps/web их не видят.

Нарушение направления зависимостей провалит `Throne.Architecture.Tests`.

## Архитектурные инварианты (forced by Throne.Architecture.Tests)

Тесты живут в `apps/api/tests/Throne.Architecture.Tests/` и запускаются как часть `backend-test-unit`. Если правило мешает — чинить код, а не тест.

- **Layer + whitelist** (`LayerDependencyRulesTests`): помимо «зависимости только внутрь» — Domain whitelist `System` + `Throne.Domain`; Application whitelist `System` + `Microsoft.Extensions` + `Throne.Application` + `Throne.Domain` + `YamlDotNet`. Новый NuGet в Domain/Application = обнови whitelist в тесте + ADR.
- **ConfigureAwait запрещён в production** (`ConfigureAwaitRulesTests`): Throne — server-side, нет SynchronizationContext. `.ConfigureAwait(...)` — шум.
- **Multi-user изоляция** (`OwnerUserIdRulesTests`, [ADR-0012](ADR/0012-throne-behind-auth-gate.md)): user-owned агрегаты обязаны принимать `ownerUserId` в `Create`/`Restore`; Mongo-документы — `[BsonElement("owner_user_id")]`; Mongo-репозитории — инжектят `ICurrentUserAccessor` и фильтруют по `owner_user_id`; Application-handler'ы создания user-owned зависят от `ICurrentUserAccessor`.
- **MCP tool registration** (`McpToolRegistrationRulesTests`, [ADR-0004](ADR/0004-mcp-call-audit-log.md)): тулы регистрируются ТОЛЬКО через `AddThroneTool<T>()` (оборачивает в `AuditingMcpServerTool`). SDK `WithTools`/`WithToolsFromAssembly` обходят аудит — запрещены. `McpServerTool.Create` вызывается только из `Throne.Api.Mcp.ThroneToolRegistration`.
- **MCP nullable parameter contract** (`McpBoundMethodParameterRulesTests`): nullable-параметр MCP-тула обязан иметь default (`= null`/`= default`); иначе `AIFunctionFactory` бросит `ArgumentException`, если клиент не прислал ключ.

## Maintainability gate (ratchet) и duplicate gate (advisory)

**`backend-maintainability` — ratchet, blocking на новых нарушениях.** Лимиты: [.quality/maintainability-budget.json](../.quality/maintainability-budget.json), профиль `legacy`. Baseline: [.quality/maintainability-baseline.json](../.quality/maintainability-baseline.json). Любое **новое** нарушение vs baseline = fail без обсуждения.

Baseline регенерируется ТОЛЬКО когда нарушения реально устранены, отдельным коммитом с rationale, не вместе с feature-работой:

```bash
bash scripts/quality/maintainability-budget-check.sh \
  --config .quality/maintainability-budget.json --profile legacy \
  --write-baseline-snapshot .quality/maintainability-baseline.json
```

**`backend-duplicates` — advisory-only.** Детектор лексический (нормализует identifiers/numbers/strings, скользит окно по логическим строкам, ловит cross-file совпадения). На текущем коде даёт много false-positive из-за идиоматических паттернов (Mongo-репозитории, Application-handlers, MVC-контроллеры). Поэтому печатает отчёт в выводе verify, но **не валит билд** и не имеет baseline. Если увидел реальную копи-пасту в отчёте — выноси в общий код по поводу, не «чтобы хэш ушёл».

## Аттачи интента (ADR-0013)

- Discovery — `get_intent.attachments[]`. У каждой записи поля `kind` (`image`/`text`/`unsupported`) и `recommended_tool` — этот тул и зови.
- `read_intent_attachment_image(intent_id, attachment_id)` отдаёт нативный image-блок (vision-tokens). Использовать только при `kind="image"`.
- `read_intent_attachment_text(intent_id, attachment_id, offset?, max_chars?)` — UTF-8 slice, `max_chars` обязательно, при `truncated=true` дочитывай со следующим `offset = returned_bytes_end`.
- MCP Resources провайдер для аттачей удалён. Никаких `intent://`-URI и `@`-mention.

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
