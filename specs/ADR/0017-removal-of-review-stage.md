# ADR-0017 — Removal of review stage in favor of chat-history transfer

Status: Accepted
Date: 2026-05-08
Supersedes (in part): [ADR-0002](0002-domain-model-and-text-versioning.md), [ADR-0003](0003-mcp-text-editing-semantics.md), [ADR-0011](0011-dream-run-model.md), [ADR-0014](0014-mcp-initialize-instructions-routing.md)

## Контекст

ADR-0002/0003 ввели training-only коллекции `intent_qa` и `intent_review` плюс соответствующие MCP-инструменты `add_intent_qa` / `add_intent_review` / `mark_ready_for_review` / `mark_ready_for_work`. Их назначение — собирать «инсайты из диалога» (interview-вопросы и post-work правки), чтобы потом dream выводил из них правила в user-инструкциях.

[ADR-0015](0015-chat-history-transfer.md) ввёл альтернативный канал доставки тех же инсайтов: пользователь штатно загружает дамп переписки с агентом (Claude Code, Claude Desktop, Codex CLI и т.п.), а локальный backend-анализатор позднее нарезает из них правки. Этот канал шире (учитывает любую дискуссию, а не только моменты, когда агент вспомнил позвать `add_intent_*`), не ломается, если MCP-клиент не вызвал нужный tool, и не кладёт ту же информацию в две коллекции.

В результате review-стадия дублирует функциональность ADR-0015 без выигрыша.

## Решение

1. **Полностью убрать** `qa` / `review` / `fix` из кодовой базы:
   - Mongo-коллекции `intent_qa` и `intent_review`, документы, репозиторий `MongoIntentTrainingRepository`, порт `IIntentTrainingRepository`, доменные `IntentQa` / `IntentReview`;
   - MCP-инструменты `add_intent_qa`, `add_intent_review`, `mark_ready_for_work` и старый поток сбора review;
   - HTTP-эндпоинты `GET /api/v1/intents/{id}/qa` и `/reviews`, DTO `IntentQaDto` / `IntentReviewDto` / `IntentTrainingAuthor`;
   - Realtime-события `intent.qa_added` и `intent.review_added`;
   - Instruction kind `fix` (включая bundle `fix`, system-instruction `fix`, режим `fix` в `get_instruction_bundle` и упоминания в mini-router'е). После feedback-а пользователя агент остаётся в режиме `work` — это покрывается обновлённым правилом в `system_instructions[kind: work]`.

2. **Сохранить** статус `ready_for_review` и MCP-tool `mark_ready_for_review`. Пользователю удобно, когда агент явно сигнализирует: «работа закончена, можно идти смотреть код». Это единственный фактический сигнал ready_for_review — авто-перехода на этот статус нет.

3. **Жизненный цикл Intent после выпила**:
   - Статусы остались как есть, минус явные `mark_ready_for_work`-вызовы.
   - Переходы `interview` / `work` выполняются автоматически при чтении соответствующего instruction bundle через `get_instruction_bundle(mode, intent_id)` (см. [ADR-0014](0014-mcp-initialize-instructions-routing.md)). Follow-up на feedback также происходит в режиме `work` — отдельного режима `fix` больше нет. Переход в `ready_for_review` остаётся явным агентским действием через `mark_ready_for_review`. Переходы в `done` / `reject` делает пользователь через UI.

4. **Dream subsystem** теперь забирает обучающий контекст только из `Intent.text` + `text_versions` (intents с `current_version > 1`) и из загруженных через transfer чат-историй. `IntentInWindow` больше не несёт `QaList` / `ReviewList`.

   **Update (ADR-0021):** после [ADR-0021](0021-insight-pipeline-and-instruction-patches.md) единственный источник обучающего материала — chat-uploads через pipeline `static extractor → local LLM grouping → InsightCard → InstructionPatch`. `Intent.text` / `text_versions` фронтиру напрямую не подаются: если паттерн привязан к конкретному intent'у, карточка указывает `target.intent_id`, и агент дочитывает intent через существующие `get_intent` / `read_intent_text`. Сущность DreamRun и связанные `intent_count > 1`-эвристики уходят вместе с супремацией ADR-0011.

## Последствия

- Существующие исторические ADR (0002/0003/0011/0014) описывают мир до выпила — оставляем их без правок, в реестре ставим отметку «частично superseded by ADR-0017».
- Один разовый дамп коллекций `qa` / `review` сохраняется в `mongo-dumps/` (gitignore), миграция «снести коллекции после деплоя» — действие оператора. Drop-индексы автоматически отвалятся вместе с самими коллекциями при старте сервера на чистой базе.
- Снижается размер MCP-toolset, что упрощает auto-injection в Claude Desktop и аналогичных клиентах. Поток interview становится односложным: агент задаёт вопрос → правит `Intent.text`, без побочного хранилища.
