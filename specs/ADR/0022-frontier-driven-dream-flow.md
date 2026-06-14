# ADR-0022: Frontier-driven dream flow вместо локального insight-pipeline

## Status

Accepted. Заменяет ранний серверный insight-pipeline (chat-uploads → static extractor → local LLM → InsightCard → InstructionPatch), поток chat-history transfer и предыдущую DreamRun-модель — см. Context.

## Context

Ранее был спроектирован серверный pipeline `chat_uploads → static extractor → local LLM (Qwen3-Coder через MLX) → InsightCard → InstructionPatch`. Реализация запускалась, фиксировались парсерные дефекты и улучшался промпт, но после фактического прогона ([intent `38ab9409`](#)) и постмортема стало ясно:

- Фронтир-модель справляется с разбором диалогов «изи» прямо в своём контексте и засоряет его меньше, чем казалось при проектировании. Static extractor + local LLM решают задачу, которой нет: лимит контекста фронтир-модели не упирался.
- На построение pipeline (vendor parsers, FIFO worker, llama.cpp adapter, парсер JSON-ответа, защита от concat/truncation, telemetry, multi-tenant изоляция) ушло непропорционально много времени относительно ценности результата.
- Local LLM (Qwen3-Coder-30B-A3B) даёт нестабильный recall на коротких операторских правилах даже после правок парсера/промпта/static-extractor — технический потолок модели.
- `ConversationFlattener` режет vendor-специфику (`tool_use`, `tool_result`, `system-reminder`, attachments), а это часть сигнала «что делал агент и как» — ровно тот контекст, который нужен, чтобы извлечь операторскую правку.
- Chat-uploads ввели целый отдельный поток данных (CLI `throne sync` + serverside vendor parsing + blob storage), но без серверного анализатора эта инфраструктура теряет смысл.

Фича не была в проде → миграция/совместимость не требуются, всё остаётся в git history.

## Decision

**Throne становится memory + patch-proposal сервисом.** Чтение диалогов уходит на сторону фронтир-агента в его раннер-окружение (`Read` / `Glob` / `Bash` локально). Сервер не принимает диалоги, не хранит их, не парсит и не вытягивает из них сигналы.

### Что остаётся на сервере

- `Instruction` + `InstructionPatch` (apply / apply-with-edit / reject) — HTTP, MCP, UI `/improvements`. Без изменений (контракт уже работал).
- Новая сущность **`DreamSession`** — append-only память о прошлых проходах: vendor, период, `processed_conversation_ids`, `summary`, `reflection`, `proposed_patch_ids`. Owner-scoped, immutable после `record`.
- Конфиг **`dream_sources`** в [specs/manifest/throne-skills.yaml](../manifest/throne-skills.yaml) — отдаёт фронтиру таблицу `{vendor → path, hint}`, откуда читать диалоги локально.

### MCP surface

Новые тулы (`Throne.Api/Mcp/Tools/DreamTools.cs`):

- `get_dream_sources()` — где у пользователя лежат диалоги по vendor.
- `list_dream_sessions(limit, cursor, vendor?)` — последние проходы для рефлексии и анти-повтора.
- `record_dream_session(vendor, date_from?, date_to?, processed_conversation_ids[], summary, reflection?, proposed_patch_ids[])` — фронтир пишет итог в конце прохода.

Сохранены без изменений: `get_current_instruction`, `propose_instruction_patch`, `list_instruction_patches`, `get_instruction_patch`.

Удалены: `list_insight_cards`, `get_insight_card`, `dismiss_insight_card`, `read_chat_span`.

### Бандл `kind: dream`

Полностью переписан под алгоритм:

1. `get_dream_sources` + `get_current_instruction(work)` + `get_current_instruction(interview)` + `list_dream_sessions(limit=5)`.
2. **Рефлексия по прошлым правкам** — `list_instruction_patches(status=applied)`, локальное чтение свежих диалогов после `applied_at`, отчёт пользователю «что прижилось, что нет».
3. Спросить vendor + период (vendor СТРОГО из `dream_sources` — иначе галлюцинация).
4. Локальное чтение диалогов через `Read`/`Glob`, фильтрация по mtime и `processed_conversation_ids` прошлых сессий.
5. Поиск операторских сигналов; обсуждение с пользователем.
6. `propose_instruction_patch(target_kind ∈ {work, interview}, ...)`.
7. `record_dream_session(...)` — обязательный финал.

Улучшаются обе user-инструкции (`work` и `interview`), а не только `work`.

### Что снесено (вместе с Mongo-коллекциями)

- Domain: `InsightCard`.
- Application: `Insights/*`, `LocalLlm/*`, `AnalysisJobs/*`, `ChatUploads/*`.
- Infrastructure: vendor parsers, blob storage, llama.cpp adapter, Mongo repositories `InsightCard` / `AnalysisJob` / `ChatUpload` / `ChatConversation`.
- API: `InsightCardsController`, `ChatUploadsController`, `AnalysisQueueController`, MCP `InsightCardTools`, `ChatSpanTools`.
- Contracts: `Throne.{Insights,ChatUploads,AnalysisQueue}.Contracts`.
- Realtime: события `insight_card.*`, `analysis_job.*`, `chat_upload.*`, `chat_conversation.*`.
- Bundle `transfer` и весь поток chat-history transfer.
- CLI `apps/cli` (sidecar `@gently-whitesnow/throne-cli` без серверного приёмника больше не нужен).
- Mongo collections: `analysis_jobs`, `insight_cards`, `chat_uploads`, `chat_conversations`, `chat_messages` — `MongoIndexInitializer` дропает их при старте как retired.

## Consequences

### Positive

- **Доменная модель Throne сжалась в разы.** Один новый append-only агрегат вместо четырёх первоклассных + цепочки worker-ов.
- **Сигнал полнее.** Фронтир видит raw JSONL с `tool_use` / `tool_result` / `system-reminder` / attachments — то, что Flattener выбрасывал.
- **Никакого ChatUpload pipeline**: ни CLI, ни serverside vendor parser, ни blob storage, ни FIFO worker, ни llama.cpp / MLX runtime, ни OpenAPI-compat порта.
- **Multi-tenancy для диалогов снимается «само»** — они не покидают машину агента.
- **Источники конфигурируемы**: `dream_sources` в манифесте + (в будущем) per-user override.
- **Recall не упирается в модель**. Используется тот же фронтир, который потом и применяет инструкции; качество сигнала растёт вместе с моделью без работы на стороне Throne.

### Negative / Risks

- **Cross-device usage**: `DreamSession.processed_conversation_ids` ссылается на локальные пути / vendor-id, которые на другой машине пользователя могут отсутствовать. Это допустимо: `summary` + `reflection` + `proposed_patch_ids` всё равно дают агенту понимание «что разбирали и какие правила приняли», даже если конкретный диалог не открыть. Сложный кросс-устройственный merge — отдельный интент, если понадобится.
- **Лимит контекста фронтира** становится практическим потолком объёма прохода. На сегодняшних моделях (Claude 4.6/4.7) хватает с запасом; на меньших — пользователь явно ограничивает периметр.
- **Нет автоматической периодической ловли инсайтов**. `dream` строго on-demand через кнопку «Скопировать промпт» на `/improvements`. Это сознательный шаг: автотриггер прошлого pipeline (cron 1h) на практике почти не давал сигнала и создавал шум в realtime-стриме.
- **Один свежий тип данных в Mongo** — `dream_sessions`. Индексы `(owner_user_id, created_at desc)` и `(owner_user_id, vendor, created_at desc)`; sharding-impact нулевой на ожидаемых объёмах.

## Migration

Не требуется. Старые коллекции `analysis_jobs`/`insight_cards`/`chat_uploads`/`chat_conversations`/`chat_messages` помечены retired в `MongoIndexInitializer` и дропаются при старте API. Если у self-hosted пользователя там были данные — это локальная эфемерная отладка периода раннего pipeline, потеря приемлема (фича не была в проде).

## References

- Постмортем + ход рассуждений: интент `d2a6c7f08cab4095bd8ac12fb4483e0c` (`derived_from` интента раннего pipeline `3a042303f99d4ddf93bbda08f6055671`).
- Реализация бандла: [`specs/manifest/throne-skills.yaml`](../manifest/throne-skills.yaml) → `system_instructions[kind: dream]` + `dream_sources`.
- HTTP контракт: [`specs/contracts/dreams/openapi.yaml`](../contracts/dreams/openapi.yaml).
- Realtime: [`specs/contracts/realtime/events.yaml`](../contracts/realtime/events.yaml) → `dream_session.recorded`.
