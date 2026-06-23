# ADR-0017 — Removal of review stage in favor of chat-history transfer

## Status

Accepted
Date: 2026-05-08
Supersedes (in part): [ADR-0002](0002-domain-model-and-text-versioning.md), [ADR-0003](0003-mcp-text-editing-semantics.md), [ADR-0014](0014-mcp-initialize-instructions-routing.md)

## Amendment (2026-06-23): статус `ready_for_review` выпилен

§ 2 этого ADR сохранял `ready_for_review` как явный агентский сигнал «работа закончена». После [ADR-0043](0043-static-operational-skills-and-mcp-removal.md) (standalone MCP retired) и [ADR-0034](0034-dual-execution-contours-hooks-vs-bundles.md) § 5 (embedded-хуки паркуют конец прохода в `awaiting_operator`) никто не эмитит `ready_for_review` автоматически, а ручной пик из дропдауна на практике не использовался. Два инбокс-статуса путали «какой именно ход за оператором», поэтому статус выпилен целиком:

- `IntentStatusNames.ReadyForReview`, ветка маппинга в `IntentStatusDtoMapper`, `IntentStatus.ready_for_review` в OpenAPI-enum и поле `inbox_review` в `IntentContextCountsDto` удалены.
- `INBOX_STATUSES` схлопнут до одного значения `awaiting_operator`; рейл инбокса показывает одну секцию.
- Quick-action «Завершить» в `SetIntentStatusForm` теперь висит на `awaiting_operator → done`.
- Миграция данных и алиас не вводятся (по аналогии с § 7 [ADR-0034](0034-dual-execution-contours-hooks-vs-bundles.md) — внешних потребителей у local-first ядра нет; живых интентов в статусе не было).

Где ниже по тексту сказано «`ready_for_review` сохраняется» / «явное агентское действие» — читать как историю до 2026-06-23.

## Контекст

ADR-0002/0003 ввели training-only коллекции `intent_qa` и `intent_review` плюс соответствующие MCP-инструменты `add_intent_qa` / `add_intent_review` / `mark_ready_for_review` / `mark_ready_for_work`. Их назначение — собирать «инсайты из диалога» (interview-вопросы и post-work правки), чтобы потом dream выводил из них правила в user-инструкциях.

Параллельно появился альтернативный канал доставки тех же инсайтов: анализ дампов переписки с агентом (Claude Code, Claude Desktop, Codex CLI и т.п.). Этот канал шире (учитывает любую дискуссию, а не только моменты, когда агент вспомнил позвать `add_intent_*`), не ломается, если MCP-клиент не вызвал нужный tool, и не кладёт ту же информацию в две коллекции.

В результате review-стадия дублирует этот канал без выигрыша.

## Решение

1. **Полностью убрать** `qa` / `review` / `fix` из кодовой базы:
   - Mongo-коллекции `intent_qa` и `intent_review`, документы, репозиторий `MongoIntentTrainingRepository`, порт `IIntentTrainingRepository`, доменные `IntentQa` / `IntentReview`;
   - MCP-инструменты `add_intent_qa`, `add_intent_review`, `mark_ready_for_work` и старый поток сбора review;
   - HTTP-эндпоинты `GET /api/v1/intents/{id}/qa` и `/reviews`, DTO `IntentQaDto` / `IntentReviewDto` / `IntentTrainingAuthor`;
   - Realtime-события `intent.qa_added` и `intent.review_added`;
   - Instruction kind `fix` (включая bundle `fix`, system-instruction `fix`, режим `fix` в `get_instruction_bundle` и упоминания в mini-router'е). После feedback-а пользователя агент остаётся в режиме `work` — это покрывается обновлённым правилом в `system_instructions[kind: work]`.

2. **Сохранить** статус `ready_for_review`. Пользователю удобно, когда агент явно сигнализирует: «работа закончена, можно идти смотреть код». Это единственный фактический сигнал ready_for_review — авто-перехода на этот статус нет.

3. **Жизненный цикл Intent после выпила**:
   - Статусы остались как есть.
   - Переходы `interview` / `work` выполняются автоматически при чтении соответствующего instruction bundle через `get_instruction_bundle(mode, intent_id)` (см. [ADR-0014](0014-mcp-initialize-instructions-routing.md)). Follow-up на feedback также происходит в режиме `work` — отдельного режима `fix` больше нет. Переход в `ready_for_review` остаётся явным агентским действием. Переходы в `done` / `reject` делает пользователь через UI.

   **Update (2026-05-13, intent e76532a0):** точечные MCP-инструменты `mark_ready_for_review` (вводился этим ADR) и `mark_needs_help` (введён в [ADR-0020](0020-intent-status-needs-help-and-fridge.md)) упразднены. Их заменил единый универсальный `set_intent_status(intent_id, status, reason?)`, дающий агенту прямой доступ к любому статусу — стееринг «какой статус ставить когда» зашит в `system_instructions[kind: common|interview|work]` и в Description тула. Триггер изменения: агент не мог самостоятельно завершить interview переходом в `ready_for_work` и тем создавал лишнее трение для оператора. Параметр `reason` опционален для любого перехода (пишется в `intent_status_changes.reason`); для `reject` обязателен и дополнительно апендится в Intent.text. HTTP-контракт `POST /intents/{id}/status` соответственно переименовал поле `reject_reason` → `reason` (источник правды — `specs/contracts/intents/openapi.yaml`). Авто-переходы `interview` / `work` при чтении bundle сохранены без изменений: они by design могут «откатить» агентский `ready_for_work` обратно на `interview` — трактуем это как «оператор снова уточняет постановку».

4. **Dream subsystem** теперь забирает обучающий контекст только из `Intent.text` + `text_versions` (intents с `current_version > 1`) и из загруженных через transfer чат-историй. `IntentInWindow` больше не несёт `QaList` / `ReviewList`.

   **Update ([ADR-0022](0022-frontier-driven-dream-flow.md)):** dream переведён на frontier-driven flow — агент читает свежие диалоги локально и предлагает патчи; `Intent.text` / `text_versions` фронтиру отдельно не подаются: если паттерн привязан к конкретному intent'у, агент дочитывает его через существующие `get_intent` / `read_intent_text`. Прежняя сущность DreamRun и `intent_count > 1`-эвристики демонтированы.

## Последствия

- Существующие исторические ADR (0002/0003/0014) описывают мир до выпила — оставляем их без правок, в реестре ставим отметку «частично superseded by ADR-0017».
- Один разовый дамп коллекций `qa` / `review` сохраняется в `mongo-dumps/` (gitignore), миграция «снести коллекции после деплоя» — действие оператора. Drop-индексы автоматически отвалятся вместе с самими коллекциями при старте сервера на чистой базе.
- Снижается размер MCP-toolset, что упрощает auto-injection в Claude Desktop и аналогичных клиентах. Поток interview становится односложным: агент задаёт вопрос → правит `Intent.text`, без побочного хранилища.
