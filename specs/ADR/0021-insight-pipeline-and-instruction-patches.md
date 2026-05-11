---
adr: 0021
title: Insight pipeline и InstructionPatch вместо DreamRun
status: Superseded by ADR-0022
date: 2026-05-09
supersedes:
  - ADR-0011
relates_to:
  - ADR-0008
  - ADR-0014
  - ADR-0015
  - ADR-0017
---

# ADR-0021 — Insight pipeline + удаление DreamRun + обновления манифеста и mini-router

> **Superseded by ADR-0022.** Pipeline признан переусложнённым; новый подход — InstructionPatch'и предлагает frontier-агент через MCP без серверного парсера диалогов.

## Status

Superseded by ADR-0022. Прежний статус — Accepted; supersedes [ADR-0011](0011-dream-run-model.md). Дополнительно правит [ADR-0014](0014-mcp-initialize-instructions-routing.md) (mini-router) и [ADR-0017](0017-removal-of-review-stage.md) (источник evidence). Опирается на [ADR-0015](0015-chat-history-transfer.md) (chat-uploads — единственный вход сырого материала) и [ADR-0008](0008-realtime-contract-first-events.md) (фанаут realtime-событий).

## Context

После [ADR-0017](0017-removal-of-review-stage.md) единственный источник материала для self-improvement loop — chat-uploads. [ADR-0011](0011-dream-run-model.md) построен вокруг сущностей `intent_qa` / `intent_review` (удалены ADR-0017) и серверного «снимка контекста», который агент получает целиком через `run_dream`. После выпила review-стадии модель DreamRun перестала описывать реальность:

1. **Источник сменился.** Вместо коротких typed-записей (`intent_qa`/`intent_review`) на входе теперь сырая переписка пользователя с агентами вендоров (Claude Code/Desktop, Codex CLI). Один upload — это десятки тысяч токенов JSON-сообщений.
2. **Снапшот в DreamRun перестал быть рабочим инструментом.** Окно «все intents с qa/review» вырождается в «все intents», а `IntentRefs[]` подаются как массив идентификаторов без шанса агенту понять, что внутри без отдельных read-вызовов. Агенту проще читать карточки/патчи самому через MCP, чем интерпретировать пустой снапшот.
3. **Frontier-агент дорогой.** Подавать ему сырой raw chat в каждом запросе нельзя — ни по латентности, ни по бюджету. Нужна явная фаза локальной обработки, которая сжимает raw в дискретные кандидаты («InsightCard»), а frontier работает уже с кандидатами.
4. **Reject без комментария бесполезен.** Без короткого «почему» система не учится не предлагать тот же мусор повторно.

## Decision

Заменяем DreamRun на pipeline `chat_uploads → InsightCard → InstructionPatch`. Frontier-агент работает с уже подготовленными карточками и патчами через MCP-инструменты; снапшот как сущность исчезает. Имя режима `dream` сохраняется в манифесте и mini-router'е для обратной совместимости конфигов и UX.

### 1. Декомпозиция pipeline по фазам

| Фаза | Что делает | Где живёт результат |
|---|---|---|
| `chat_uploads` | Пользователь грузит дамп через CLI (ADR-0015). | Архив на host-volume + метаданные в Mongo (`chat_uploads`). |
| `vendor parser` | Парсит формат вендора (`claude` / `codex`) в **общую timeline-модель** сообщений и tool-calls. **Raw-формат записывается в Mongo как есть, без нормализации**. Цель — чтобы `read_chat_span` мог отдать любой кусок диалога без обращения к архиву на диске. | Коллекция `chat_conversations`: один документ на conversation, raw vendor-сообщения как embedded массив `vendor_payload[]` (`{ index: int, raw: string }`). Ключ — `(upload_id, vendor, conversation_id)` (uniq), индексы по `owner_user_id`, `upload_id`, `(owner_user_id, vendor, conversation_id)`. Отдельной коллекции `chat_messages` нет — span-доступ работает по `index` внутри embedded-массива. |
| `static extractor` | Эвристика по **структурным сигналам** (см. §7), генерирует кандидатов InsightCard со span-ссылкой на конкретные сообщения внутри `chat_conversations.vendor_payload`. | Коллекция `insight_cards` (status=`candidate`, source=`static`). |
| `local LLM grouping` | Локальная LLM (Qwen3-Coder через MLX, см. §3) группирует/обогащает кандидатов: дедуп близких карточек, добавление `target` (какой kind инструкции и/или intent затрагивает), `confidence`. | Те же `insight_cards`, status=`ready` или `merged`. |
| `frontier MCP` | Frontier-агент (Claude Opus и аналоги) через MCP читает `list_insight_cards` / `read_chat_span` и формирует `InstructionPatch`. | Коллекция `instruction_patches` (status=`proposed`). |
| `user decide` | Пользователь применяет / редактирует / отклоняет патч в UI. Apply вызывает `IInstructionRepository.ReplaceTextAsync` (тот же путь, что и DreamRun-apply в ADR-0011). | `text_versions` инструкции. |

Pipeline **асинхронный**. `analysis_job` — отдельная сущность, которая координирует прохождение раундов; на каждый upload автоматический re-run **не делается** — только cron 1h или on-demand через UI/MCP.

### 2. Multi-tenancy + видимая глобальная FIFO

Вычислительные ресурсы (local LLM, frontier токены) общие. Поэтому `analysis_job` встаёт в **глобальную очередь**, и её позиция/фаза публичны (видны всем пользователям как «вы N-й в очереди, текущая фаза — `local-llm-grouping`»). Содержимое самих jobs / cards / patches остаётся приватным владельцу и фильтруется по `OwnerUserId` (как в ADR-0012). Это решает «когда же оно отработает» без раскрытия чужих данных.

### 3. MLX (mlx-lm) как дефолт runtime локальной LLM

Локальная LLM запускается через **MLX** (Apple-native фреймворк для Apple Silicon), сервер — `mlx_lm.server` из пакета [`mlx-lm`](https://github.com/ml-explore/mlx-examples/tree/main/llms). Базовая модель — `mlx-community/Qwen3-Coder-30B-A3B-Instruct-4bit` (MoE 30B/3B-активных, ≈17.5 GB Q4 в unified memory). Доступ — через **OpenAI-compatible порт** `IChatCompletionPort` (Application). Существующая реализация (исторически `LlamaCppChatCompletionAdapter`) — тонкий OpenAI-compat HTTP клиент: `mlx_lm.server` обслуживает тот же `/v1/chat/completions`, поэтому **код адаптера не меняется**; меняется только `Throne:LocalLlm:BaseUrl` (default — `http://127.0.0.1:11434/v1`, чтобы LAN-доступ к удалённой Mac Studio-инстансу шёл по тому же порту) и `Throne:LocalLlm:Model`. Менять модель — replace через DI на удалённую API-реализацию того же порта (`OpenAiChatCompletionAdapter` / `AnthropicChatCompletionAdapter`) без изменений вызывающего кода.

**Почему MLX, а не llama.cpp/Ollama.** Независимые бенчмарки на Apple Silicon (2026) показывают для Qwen3-Coder-30B-A3B 3× разрыв в throughput: MLX ~130 tok/s (M4 Pro) / ~230 tok/s (M2 Ultra) против Ollama/llama.cpp ~43 tok/s. Причины: MoE-архитектура (3B активных) лучше использует unified memory MLX, нативные Metal-кернелы без слоя GGML, меньший footprint (34.7 GB Q4 MLX против 40 GB GGUF). В марте 2026 Ollama объявила миграцию на MLX backend — то есть выбор MLX совпадает с траекторией экосистемы. Раннее обсуждение в этом ADR (первоначально — `llama.cpp Metal`) пересмотрено 2026-05-11 до начала реализации; код адаптера от выбора не зависит, поэтому правка — без миграции.

**Развёртывание.** Headless `mlx_lm.server` на Mac Studio в systemwide LaunchDaemon (см. `throne-infra/bootstrap.mlx.sh`). Bind — `0.0.0.0:11434`, чтобы инстанс был доступен и локально (compose dev), и из LAN, и из прод-deploy на той же машине через loopback. Скачанные веса в MLX-формате лежат в `~/.cache/huggingface` (унифицировано с другими HF-моделями) — не пересекаются с `~/.ollama/models` (GGUF).

### 4. InsightCard и InstructionPatch — first-class сущности

**InsightCard** (`insight_cards`):

- `id`, `owner_user_id`, `created_at`, `updated_at`;
- `source`: `static` | `local_llm` | `merged`;
- `status`: `candidate` | `ready` | `merged` | `dismissed` | `consumed`;
- `target`: `{ kind: common|interview|work|dream|transfer, intent_id?: string }` — что предположительно подлежит правке;
- `confidence`: int 0..100;
- `summary`: ≤280 символов, человекочитаемое описание паттерна;
- `spans[]`: ссылки на конкретные сообщения внутри `chat_conversations` (`upload_id`, `vendor`, `conversation_id`, `start_index`, `end_index` — индексы в `vendor_payload[]`) — **не копия**, а ссылка для `read_chat_span`;
- `dismiss_comment?`: причина отклонения (≥10 символов, обязательно при `dismissed`);
- `merged_into?`: id карточки, в которую слилась.

Жизненный цикл: `candidate → ready → (consumed | dismissed | merged)`. `consumed` — карточка использована в одном из применённых `InstructionPatch`. `dismissed` — пользователь явно отклонил карточку (через `dismiss_insight_card`).

**InstructionPatch** (`instruction_patches`):

- `id`, `owner_user_id`, `created_at`, `updated_at`;
- `status`: `proposed` | `applied` | `edited_and_applied` | `rejected` | `superseded`;
- `target_kind`: `common` | `interview` | `work` | `dream` | `transfer`;
- `base_instruction_version`: int — версия инструкции, на которую агент опирался;
- `proposed_text`: предлагаемое новое содержимое (целая инструкция или delta — формат уточняется в реализации; в первой итерации — целый текст после `## Learned rules` injection, по аналогии с DreamRun-apply из ADR-0011);
- `final_text?`: что именно применил пользователь (может отличаться от `proposed_text` — режим `edited_and_applied`);
- `confidence`: int 0..100;
- `rationale`: объяснение агента (≤500 символов);
- `evidence_card_ids[]`: список `InsightCard.id`, на которые опирается патч (subset выданных через MCP);
- `reject_comment?`: причина отклонения (≥10 символов, обязательно при `rejected`).

Жизненный цикл: `proposed → (applied | edited_and_applied | rejected | superseded)`. Apply путь — пользовательское действие через UI / HTTP, **не агентское** (как в ADR-0011 — apply остаётся за человеком). После apply все `evidence_card_ids` карточки автоматически переводятся в `consumed`.

`base_instruction_version` валидируется при apply ровно как в ADR-0011 — несовпадение → `409 instruction_patch.needs_rebase` без мутации.

### 5. MCP surface для frontier-агента

Новые tools (под существующим Throne MCP-сервером, регистрация — стандартный `AddMcpTool` pattern):

- `list_insight_cards(filter: { status?: ready|consumed|dismissed; target_kind?; intent_id?; min_confidence? }, limit, cursor)` — пагинированный список своих карточек.
- `get_insight_card(card_id)` — полное содержимое карточки.
- `read_chat_span(upload_id, vendor, conversation_id, start_index?, end_index?)` — раздаёт окно raw сообщений из embedded `vendor_payload[]` соответствующего `chat_conversations`-документа (с проверкой `OwnerUserId`). Используется агентом, чтобы прочитать конкретные доказательства из карточки.
- `list_instruction_patches(filter: { status?, target_kind? }, limit, cursor)` — пагинированный список своих патчей (включая ранее `rejected` с `reject_comment` — для дубликат-проверки).
- `get_instruction_patch(patch_id)`.
- `propose_instruction_patch(target_kind, base_instruction_version, proposed_text, rationale, evidence_card_ids[], confidence)` — создаёт новый патч в статусе `proposed`.
- `dismiss_insight_card(card_id, comment)` — переводит карточку в `dismissed` с обязательным комментарием ≥10 символов.
- `get_current_instruction(target_kind)` — отдаёт текущий текст user-инструкции данного kind (read-only). Нужно агенту, чтобы видеть, на чём строится `base_instruction_version`.

Удаляются: `run_dream` и `propose_dream_rule` (на surface остаются только до релиза ADR-0021; в этом ADR фиксируется решение, фактическое удаление — отдельный реализационный intent).

### 6. Принцип: фронтир не получает snapshot

В отличие от `run_dream` из [ADR-0011](0011-dream-run-model.md), который отдавал агенту `evidence_summary` со снимком и `intent_refs[]`, ADR-0021 не вводит сущность «снапшот контекста». Frontier сам пагинирует `list_insight_cards` / `list_instruction_patches`, сам решает, что прочитать через `read_chat_span`. **Лимиты выбирает пользователь в запросе** («возьми 20 последних карточек с confidence ≥ 70»). Это:

- убирает серверный «cap» из ADR-0011 (был «без cap», что в новых объёмах chat-uploads нерационально);
- даёт пользователю прямой контроль над затратами frontier;
- избавляет от состояния DreamRun (создан/закрыт/auto-close): вся координация — через статусы карточек и патчей.

### 7. Static extractor — только структурные сигналы

Static-фаза MVP **не использует словарные эвристики** (поиск по словам «error», «всегда», «никогда», «правило» и т.п.). Только структурные паттерны:

- Failed tool calls (`mcp_call_log.outcome=error` в дампе или явная ошибка вендора);
- Длинный user-message сразу после короткого assistant-message (типичный паттерн «нет, ты сделал не то»);
- Несколько идущих подряд правок одного и того же файла без assistant-комментариев между ними;
- Ручной откат (`git reset` / откат в редакторе), если детектируется в сессии.

Слова-эвристики намеренно исключены — они уходят в фазу `local LLM grouping`, где у локальной модели есть контекст вокруг паттерна.

### 8. Reject обязателен с комментарием на двух уровнях

- Карточка через `dismiss_insight_card(card_id, comment)` — `comment` ≥10 символов.
- Патч через UI/HTTP `reject(patch_id, comment)` — то же требование, валидация на MCP/HTTP границе.

Минимальная длина — конкретное число (10 символов), чтобы не пропускать пустые «.» / «нет». Комментарии живут как часть состояния карточки/патча и должны учитываться frontier'ом при следующем `propose_instruction_patch` (см. обновление `system_instructions[kind: dream]` в манифесте — §Manifest ниже).

## Обновления существующих ADR

### ADR-0011

Помечается **Superseded by ADR-0021** в шапке. Контент остаётся как историческая запись о промежуточной модели DreamRun. Все ссылки на `intent_qa` / `intent_review` к моменту supersession уже сняты ADR-0017.

### ADR-0017

Точка «Dream subsystem теперь забирает обучающий контекст только из `Intent.text` + `text_versions` (intents с `current_version > 1`) и из загруженных через transfer чат-историй» уточняется примечанием: после ADR-0021 источник — **только chat-uploads через InsightCard pipeline**. `Intent.text` / `text_versions` фронтиру напрямую не подаются: если паттерн привязан к конкретному intent'у, карточка указывает `target.intent_id`, и агент может прочитать его через существующий `get_intent` / `read_intent_text`.

### ADR-0014

В mini-router добавляется строка для нового потока (имя режима `dream` сохраняется для совместимости конфигов и манифеста):

```
- improve user instructions from accumulated chat insights → mode="dream"
  (uses list_insight_cards + propose_instruction_patch; no DreamRun)
```

Канонический текст — константа `ThroneServerInstructions.MiniRouter`. Правка mini-router'а — отдельный реализационный коммит, документируется этим ADR.

## Manifest

В `specs/manifest/throne-skills.yaml` `system_instructions[kind: dream]` переписывается под новый flow:

- Алгоритм: `list_insight_cards` (≤N последних `ready`, лимит выбирает пользователь в запросе) → опциональный `read_chat_span` для контекста → `list_instruction_patches(status=rejected)` для проверки, не предлагалось ли похожее с rejected-комментарием → `get_current_instruction(target_kind)` → `propose_instruction_patch`.
- Перед предложением патча проверить недавние `instruction_patches` в статусе `rejected` (с учётом `reject_comment`) и недавние `dismissed` карточки — не повторять отклонённое.
- Агент проставляет `confidence` 0..100 в самом патче (и при необходимости — в карточках, если возвращает их в `merged` обратно в pipeline; в MVP — только на патчах).
- Сохраняется запрет на toxic absolutism, на apply «своих» предложений и на изменение кода. Bundle `dream` в манифесте остаётся (`mode: dream` → `system:common + system:dream + user:common + user:dream`); меняется только текст `system:dream`.

## REGISTRY.md

Добавляется запись ADR-0021. Статусы:

- ADR-0011 → отметка «Superseded by ADR-0021».
- ADR-0017 / ADR-0014 → отметка «дополнительно уточняется ADR-0021» в их строках реестра (в частях, перечисленных выше).

## Realtime contracts

В `specs/contracts/realtime/events.yaml` поэтапно (вне scope этого ADR — отдельные реализационные intents) появятся:

- `insight_card.created`, `insight_card.updated`, `insight_card.dismissed`, `insight_card.merged`, `insight_card.consumed`;
- `instruction_patch.proposed`, `instruction_patch.applied`, `instruction_patch.rejected`, `instruction_patch.superseded`;
- `analysis_job.created`, `analysis_job.phase_changed`, `analysis_job.completed`, `analysis_job.failed`.

Удалятся: `dream.run_created`, `dream.proposal_created`, `dream.proposal_applied`, `dream.proposal_skipped`, `dream.run_closed`, `dream.fuel_changed`.

В этом ADR-коммите в `events.yaml` добавляется только `future_events:` секция-документ (парсер её не читает) и комментарий-deprecation на `dream.*` записях. Реальный move событий + соответствующие домен-event records, RealtimeDomainEventHandler-кейсы, `useRealtimeEvent` подписки и openapi-схемы для DTO появляются в отдельных реализационных intents — иначе realtime gate ADR-0008 справедливо упадёт.

## Не делаем

- **Cursor / Cline / Aider парсеры.** В первой итерации только `claude` / `codex` (как в ADR-0015).
- **Авто-применение патчей.** Apply — всегда явное user-action.
- **Soft-delete карточек / патчей.** Только статусы `dismissed` / `rejected` с комментарием. Hard-delete — отдельная админ-операция вне scope.
- **Cross-user шаринг карточек или патчей.** Изоляция по `OwnerUserId` строгая.
- **ML-дедуп / embeddings.** В MVP дедуп — лексический в фазе local LLM grouping (модель сама группирует похожее в своём output-е). Embeddings-индекс — future ADR.
- **Session-aware фильтр** («не учитывать активную сейчас сессию»). Полагаемся на ручной запуск пользователем.
- **Token cap у фронтир-сессии.** Полагаемся на limit карточек, который пользователь сам указывает в запросе.
- **Авто re-run анализа на каждый upload.** Только cron 1h или on-demand. Это снимает проблему «пользователь грузит 10 архивов подряд → 10 параллельных раундов».

## Альтернативы (отвергнуто)

1. **Оставить DreamRun, поменять только источник evidence на chat-uploads.** Сущность «снапшот контекста» теряет смысл, когда сырой материал — это десятки тысяч токенов raw JSON: снимок не помещается в один MCP-вызов, а frontier'у всё равно приходится читать его кусками. DreamRun становится пустой обёрткой над списком id-шников.
2. **Подавать сырые сообщения сразу frontier'у (без InsightCard).** Слишком дорого по токенам, нестабильно (frontier тратит контекст на парсинг сырых JSON-структур вендоров) и невозможно дедуплицировать между раундами.
3. **Считать `confidence` автоматически на стороне сервера.** В MVP frontier — единственный, у кого есть все данные для оценки confidence (карточки + история отклонённых патчей + текущая инструкция). Серверная эвристика будет мимо без обучения.
4. **Делать static extractor шире (включая словарные эвристики).** Шум растёт быстрее, чем сигнал; локальная LLM делает ту же работу качественнее на меньшем числе кандидатов.
5. **Использовать удалённую LLM для grouping вместо локальной.** Раунд анализа становится дорогим и асинхронно недетерминированным по бюджету. Локальная LLM на Mac (Qwen3-Coder через MLX) даёт латентность секунд и нулевой денежный кост. Порт `IChatCompletionPort` оставляет возможность переключиться позже.

6. **llama.cpp Metal как раннер (первоначальный выбор этого ADR).** Пересмотрено 2026-05-11: на Qwen3-Coder-30B-A3B MLX даёт ~3× throughput и -13% памяти (см. §3). Код адаптера от выбора не зависит — порт OpenAI-compat одинаковый.

7. **Ollama как раннер.** Ollama — обёртка над llama.cpp с менеджментом моделей и удобным `pull`. Throughput на тестовой модели ~43 tok/s (M4 Pro) — в 3× медленнее MLX. Сама Ollama в марте 2026 объявила миграцию на MLX backend, то есть в среднесроке преимущество DX без потери скорости вернётся — тогда вернёмся к вопросу. В MVP оптимизируем под throughput.

## Consequences

### Positive

- Раздельные роли: static extractor — структурные сигналы (детерминированный, дешёвый), local LLM — группировка и target/confidence (специализированная, локальная), frontier — только финальное предложение патча (дорогой, минимальный объём контекста).
- Frontier работает с дискретными карточками, а не с raw чатами, — стоимость и латентность раунда контролируемы.
- Видимая FIFO-очередь снимает «черноту» pipeline без раскрытия данных других пользователей.
- Reject-comment становится обучающим сигналом для следующего раунда.
- `IChatCompletionPort` оставляет дверь открытой для удалённых API-моделей без переписывания вызывающего кода.

### Negative / Risks

- **Сложнее в реализации, чем DreamRun.** Появляются три новых coll. (`chat_conversations`, `insight_cards`, `instruction_patches`), `analysis_job`, локальный llama.cpp, новый MCP surface. Это разбивается на отдельные реализационные intents (см. блокирующий `6d485bda6be84b7ca06e17131058d8d2`).
- **Локальная LLM — внешняя зависимость хоста.** Если `mlx_lm.server` не запущен, фаза grouping встаёт. Митигация: pipeline переживает паузу, `analysis_job` остаётся в статусе `waiting_for_local_llm`, видна пользователю в FIFO.
- **Глобальная FIFO видит позицию между tenant'ами.** В однопользовательском self-host это нерелевантно; для будущего multi-tenant — приемлемая утечка (только позиция и фаза, не содержимое).
- **`base_instruction_version` race.** Решается тем же `409 needs_rebase`, что и в ADR-0011.
