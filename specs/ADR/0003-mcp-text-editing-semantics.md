# ADR-0003: MCP text-editing semantics

## Status

Accepted (amended семь раз: (1) убраны `*_from_interview` write-tools, введён отдельный `add_intent_qa`; (2) убран `reason?` из edit-tools, тип возврата write-tools обновлён под доменную модель ADR-0002 после её аменда — без `qa[]`/`review[]`; (3) `text_versions` перешёл на delta-формат — описание записи в `replace`/`insert` обновлено под ADR-0002 §4; (4) tool surface сужен до 12 штук — выкинуты `list_*`, `get_instruction(id)`, `read_instruction_text` / `search_instruction_text` и `include_text?` флаг как избыточные для MVP-flows из [readme.md](../../readme.md); (5) write-tools для Instruction убраны — инструкции в MVP редактируются пользователем напрямую (mongosh / будущий HTTP-эндпойнт), агент только читает через bundle; (6) для UI введены HTTP read-эндпойнты `GET /api/v1/instructions` и `GET /api/v1/instructions/{id}` (плюс симметричный `GET /api/v1/intents/{id}`) — read-only surface для фронта `apps/web`; (7) для UI введён write-surface через HTTP: `POST /api/v1/intents`, `POST /api/v1/intents/{id}/replace-text`, `DELETE /api/v1/intents/{id}` (каскадно удаляет `text_versions` агрегата), `POST /api/v1/instructions/{id}/replace-text`, плюс `GET /{id}/versions` для обоих агрегатов. `changed_by=user` для всех HTTP-write вызовов, MCP-агент по-прежнему пишется как `agent`. Write через MCP для Instruction **по-прежнему запрещён** — пользователь правит через UI, агент только читает.)

## Context

[ADR-0002](0002-domain-model-and-text-versioning.md) зафиксировал доменную модель: `Intent` / `Instruction` с canonical `text` и `current_version`, единая коллекция `text_versions` в delta-формате (v1 snapshot + v2+ delta-записи), отдельные коллекции `intent_qa` / `intent_review` (агенту невидимые), обязательный `expected_version` на write-tools. Открытым остался контракт MCP tools, через который агент будет фактически работать с `Intent.text` и `Instruction.text`.

Миссия и границы MVP из [readme.md](../../readme.md) задают минимальный dogfooding-surface, но оставляют открытыми операционные детали MCP tools:

- что считается «слишком большим ответом» при чтении без `line_count`;
- какой формат actionable error достаточен, чтобы агент сам решил, как действовать (расширить `old_text`, перечитать диапазон, использовать `search`);
- как фиксируется пара вопрос/ответ из interview относительно правок text;
- как выбирается bundle инструкций для каждого режима — на стороне агента или сервера.

Рассмотренные альтернативы:

1. **Сцепить qa с каждой text-правкой через `*_from_interview` варианты (как было в первой редакции этого ADR).** Это вынуждает писать qa **на каждую** правку, что ломается на типичных interview-сценариях: один ответ → несколько правок (qa дублируется), один ответ → ноль правок (qa некуда записать), фикс опечатки без вопроса (фейковая пара). Отклонено в пользу decoupled-модели: отдельный `add_intent_qa` + обычные edit-tools без знания о режиме (см. §3).
2. **Возвращать unified diff из `replace_*_text` ошибок.** Богаче, но требует от агента парсинга diff. Достаточно структурированного `error.detail` с полями `matches_count`, `near_lines[]`, `hint`. Отклонено в пользу простого actionable JSON.
3. **Сборка bundle инструкций на стороне агента (агент сам зовёт `list_instructions(kind=common)` + `list_instructions(kind=interview)`).** Открывает риск, что агент забудет `common`. Выбрано: серверный `get_instruction_bundle(mode, intent_id?)`.
4. **Поддержать `replace_by_line_range` для удобства.** Номера строк дрейфуют между версиями, гонки сложнее воспроизводимы. Отклонено в пользу byte-exact `replace_intent_text`.
5. **Поддержать `full_replace` как обычный tool.** Слишком легко превращается в потерю контекста для больших документов. Отклонено как обычный tool; полная перезапись возможна только косвенно через `replace_*_text` с `old_text == текущий весь текст`, что само по себе требует от агента осознанности.

6. **Объём tool surface — реализовать расширенный набор или сузить до minimum для dogfooding.** Расширенный вариант включал `list_*`, `get_instruction(id)`, `read_instruction_text` / `search_instruction_text`, `include_text?` флаг. Ни один из этих tools не нужен для MVP-flows из readme. Браузить список intent'ов из агента — нет сценария (агент работает по `intentId` от пользователя). Читать одну инструкцию по id — нет сценария (агент берёт инструкции **только** через `get_instruction_bundle(mode, intent_id?)`). Range-read для инструкций — оверкилл, потому что seed-инструкции короткие и bundle отдаёт их целиком. История версий через MCP — нет сценария (читается ETL-скриптами). Выбрано: minimum.

7. **Агентское редактирование инструкций.** В MVP пользователь правит инструкции **напрямую** — через `mongosh` или будущий HTTP-эндпойнт в `Throne.Api`. Агентский edit инструкций (через MCP) сознательно не вводится: в границах MVP readme это прямо запрещённый surface; цена — три лишних tool'а (`create_instruction`, `replace_instruction_text`, `insert_instruction_text_after_line`) с дублирующими тестами и лишним surface для агентских ошибок. Когда придёт сценарий «агент должен сам подкручивать инструкции по ходу dogfooding» — отдельный ADR.

## Decision

### 1. Набор tools (final)

MVP-набор — 9 tools, ровно столько, сколько нужно для slash-команд `/tinterview`, `/twork`, `/tnew`, `/treview`. `list_*`, `get_instruction(id)`, `read_instruction_text` / `search_instruction_text`, `include_text?` флаг и **write-tools для Instruction** сознательно не вводятся (см. альтернативы в Context). Инструкции редактируются пользователем напрямую (mongosh / будущий HTTP-эндпойнт), агент через MCP их только читает.

Чтение (4):

```text
get_intent(intent_id) -> IntentWithText
read_intent_text(intent_id, start_line? = 1, line_count?, max_chars?) -> TextSlice
search_intent_text(intent_id, query, context_lines? = 3, limit? = 10) -> TextSearchResult[]
get_instruction_bundle(mode, intent_id?) -> InstructionBundle
```

`get_intent` всегда возвращает `text` — у агента нет MVP-сценария, где Intent читается без него. Ни `get_intent`, ни `read_intent_text` qa/review не возвращают (см. ADR-0002 §5: они хранятся в `intent_qa` / `intent_review` исключительно как training-only данные).

`get_instruction_bundle(mode, intent_id?)` — единственный путь работы агента с инструкциями. Возвращаемые `InstructionWithText` несут `kind`, `instruction_id`, `current_version`, `text`. `intent_id` опционален и нужен для audit-связки `InstructionBundleUse`, когда Intent уже известен: какой Intent работал под какими instruction-версиями.

Запись Intent (5):

```text
create_intent(text, tags?) -> Intent
replace_intent_text(intent_id, expected_version, old_text, new_text) -> Intent
insert_intent_text_after_line(intent_id, expected_version, after_line, insert_text) -> Intent
add_intent_qa(intent_id, expected_version, question, answer) -> Ack
add_intent_review(intent_id, expected_version, note, reason) -> Ack
```

`Ack` — компактный ответ `{ intent_id, current_version, accepted: true }`. qa/review не возвращаются назад агенту.

`add_intent_qa` — отдельный tool, не флаг и не часть edit-операций. Edit-tools о режиме работы (interview / light_work / new_project) ничего не знают и в `intent_qa` не пишут. Подробности связи qa ↔ правки — §5.

Параметра `reason?` на edit-tools нет: «зачем агент это сделал» уже выводимо из `mcp_call_log` (session_id + tool_name + arguments по timestamp) и из `intent_qa` для interview-сессий. Поле `reason` остаётся только в `add_intent_review` как контентное (см. §6).

Запись Instruction через MCP **отсутствует**. Для bootstrap инициализации работает seed-логика (отдельный ADR), для дальнейших правок — пользователь напрямую через `mongosh` или (когда появится) HTTP-эндпойнт в `Throne.Api`. Это закрывает цикл «инструкция → bundle → агент» без необходимости MCP write-surface для инструкций.

`replace_by_line_range` и `full_replace` как tools **не вводятся**. Полная перезапись большого документа не поддерживается серверным API; если нужно — агент должен сначала прочитать `read_*_text(line_count = total_lines)` и затем сделать `replace_*_text` с известным `old_text`.

### 2. Контракт чтения

`read_*_text(id, start_line?, line_count?, max_chars?)`:

- `start_line` — 1-indexed, дефолт = 1.
- `line_count` без значения — сервер возвращает с конца документа `total_lines - start_line + 1` строк, но обязан применить серверный лимит `max_chars`.
- `max_chars` — клиентский потолок. Серверный жёсткий лимит = 64 000 символов на ответ. Если `max_chars` не передан, применяется серверный. Если требуется больше — агент должен идти диапазонами.
- Ответ:
  ```text
  TextSlice
  - current_version : int
  - start_line      : int
  - end_line        : int
  - total_lines     : int
  - content         : string
  - truncated       : bool       // true если ответ был обрезан по max_chars
  - next_start_line : int?       // подсказка для пагинации, если truncated
  ```

`search_*_text(id, query, context_lines?, limit?)`:

- Поиск — case-sensitive, по подстроке. Regex в MVP не вводится.
- `context_lines` дефолт = 3, `limit` дефолт = 10, серверный max = 50.
- Ответ:
  ```text
  TextSearchResult
  - match_line   : int
  - match_column : int
  - context      : string        // строки [match_line - context_lines .. match_line + context_lines]
  - context_start_line : int
  ```
- Если общее число совпадений > `limit`, в ответе поле `total_matches_estimate` и hint «уточни запрос или используй `search_*_text(query)` с более специфичной подстрокой».

### 3. Контракт точной замены

`replace_*_text(id, expected_version, old_text, new_text)`:

- `expected_version` обязателен.
- `old_text` должен встречаться в текущем `text` ровно один раз. Сравнение byte-exact: whitespace, переносы строк, BOM значимы.
- При успехе сервер атомарно: меняет `text`, инкрементирует `current_version`, пишет в `text_versions` delta-запись `kind = replace` с `old_text` / `new_text` ровно как пришли (см. ADR-0002 §4), обновляет `updated_at`. Коллекции `intent_qa` / `intent_review` не трогаются — для них есть отдельные tools (§5, §6).
- Ошибки — typed `ApiException` с одним из кодов:

  | Код | Когда | Detail |
  |---|---|---|
  | `intent.version_conflict` | `expected_version != current_version` | `current_version`, `expected_version` |
  | `intent.text.match_not_found` | `old_text` не найден | `query_preview` (первые 80 символов `old_text`), hint «используй `search_intent_text` или расширь `old_text` соседним контекстом» |
  | `intent.text.match_ambiguous` | `old_text` найден >1 раза | `matches_count`, `match_lines[]` (первые до 5 строк, где найдены совпадения), hint «расширь `old_text`, чтобы он стал уникальным» |

  Префикса `instruction.*` нет: write-tools для Instruction отсутствуют (см. §1). Если в будущем появятся — добавятся симметричные коды.

- Все коды собираются в едином реестре `Throne.Application.ErrorCodes`. Снаружи ошибки отдаются через единый Problem Details writer (`urn:throne:error:<code>`), как требует common seed-инструкция.

### 4. Контракт вставки после строки

`insert_*_text_after_line(id, expected_version, after_line, insert_text)`:

- `after_line = 0` — вставка в начало документа.
- `after_line` валиден на диапазоне `0 .. total_lines` текущей версии. Out-of-range → `intent.text.line_out_of_range` с detail `total_lines`, `requested_after_line`.
- `insert_text` может быть многострочным. Сервер не добавляет автоматический перенос строки между `insert_text` и следующей строкой — корректность переносов на агенте.
- При успехе — атомарно: обновление `text`, инкремент `current_version`, delta-запись в `text_versions` с `kind = insert`, `after_line` / `insert_text` (см. ADR-0002 §4). `intent_qa` / `intent_review` не трогаются.

### 5. qa

`add_intent_qa(intent_id, expected_version, question, answer) -> Ack`:

- `expected_version` обязателен. qa-запись **не** инкрементирует `current_version` (qa — не правка `text`), но `expected_version` проверяется, чтобы агент не наслаивал qa поверх текста, который параллельно был перезаписан.
- При успехе сервер атомарно: проверяет `expected_version == current_version`, вставляет документ в коллекцию `intent_qa` с `intent_version_at_write = current_version`, серверным `created_at`, обновляет `Intent.updated_at`.
- Возврат — `Ack { intent_id, current_version, accepted: true }`. Сам qa-документ агенту не возвращается (агент в принципе qa не читает, см. §1).
- Конфликт → `intent.version_conflict`.

Связь qa ↔ правка text **не** хранится явной ссылкой. Восстанавливается по timestamp при анализе dogfooding-данных, и этого достаточно для цели «собирать материал для будущего обучения». Это сознательное упрощение MVP: явная ссылка усложнила бы edit-tools и заставила бы их знать о режиме работы агента.

Защита от «агент забыл вызвать `add_intent_qa` после ответа пользователя» — двухслойная и не блокирующая на уровне сервера:

1. `Instruction(kind: interview)` явно предписывает: после каждого ответа пользователя — сначала `add_intent_qa`, затем правка `Intent.text`. Это часть seed-инструкций (см. ADR-0005).
2. Серверная телеметрия из ADR-0004 («MCP call audit log») позволяет в dogfooding посчитать долю text-правок без сопряжённого `add_intent_qa` за окно ±N секунд в interview-сессиях. Это сигнал для улучшения инструкций, а не runtime-валидация.

Жёсткую серверную проверку «нельзя править без свежего qa» сознательно не вводим: режим работы (interview vs light_work vs new_project) серверу неизвестен, и такая проверка наложила бы runtime-ограничение поверх семантики, которая существует только на уровне agent instruction.

### 6. Review

`add_intent_review(intent_id, expected_version, note, reason) -> Ack`:

- `expected_version` обязателен (см. ADR-0002 §6).
- Запись review **не** инкрементирует `current_version`. Операция атомарно: проверяет `expected_version == current_version`, вставляет документ в `intent_review` с `intent_version_at_write = current_version`, серверным `created_at`, обновляет `Intent.updated_at`.
- `note` — само замечание; `reason` — содержательное поле «почему это важно / что AI понял неправильно», часть продуктовой семантики, а не метаданные операции.
- Возврат — `Ack`, сам review-документ агенту не возвращается.
- Конфликт → `intent.version_conflict`.

### 7. Bundle инструкций

`get_instruction_bundle(mode, intent_id?)` — единственный поддерживаемый способ получить набор инструкций для режима. Маппинг режимов жёстко зашит на сервере:

```text
mode = interview     -> [common, interview]
mode = light_work    -> [common, light_work]
mode = new_project   -> [common, new_project]
```

Сервер возвращает `InstructionBundle { mode, intent_id?, instructions, missing_kinds }`, где `instructions[]` содержит `InstructionWithText` (`kind`, `instruction_id`, `current_version`, `text`). Если для нужного `kind` нет ни одной инструкции (что не должно случаться благодаря seed bootstrap — будущий ADR-0005) — сервер возвращает то, что есть, и явный flag `missing_kinds[]` в ответе, чтобы агент мог сообщить пользователю.

Slash-команды на стороне агента маппятся на режимы:

```text
/tinterview -> get_instruction_bundle(interview, intent_id?)
/twork      -> get_instruction_bundle(light_work, intent_id?)
/tnew       -> get_instruction_bundle(new_project, intent_id?)
/treview    -> get_instruction_bundle(light_work, intent_id?)
```

Этот маппинг — часть agent instruction, а не серверного API. Сервер не парсит slash-команды.

### 8. Политика возврата

- `get_intent(intent_id)` всегда возвращает `IntentWithText { intent_id, current_version, tags, text, created_at, updated_at }`. Без флагов, без вариаций.
- `get_instruction_bundle(mode, intent_id?)` возвращает `InstructionBundle` с `InstructionWithText { kind, instruction_id, current_version, text }`. Агент использует это только для подмешивания инструкций в свой контекст; редактировать инструкции через MCP он не может (см. §1).
- Большие Intent'ы агент читает диапазонами через `read_intent_text`. Серверный лимит ответа — 64 000 символов (см. §2).
- qa, review, история версий **не выставляются через MCP**. Они хранятся в `intent_qa` / `intent_review` / `text_versions` и доступны только ETL-скриптам напрямую из Mongo (см. ADR-0002 §5 и §4).
- list-tools (`list_intents`, `list_instructions`, `list_*_versions`) в MVP не вводятся: MVP-flows из readme их не требуют. Когда понадобятся — отдельный ADR.
- Отдельный параметр `response_format` в API не вводится: один стабильный structured response проще для агента.

## Consequences

### Positive

- Контракт ошибок achievable в 3 кодах на каждый агрегат: достаточно для actionable retry-логики агента, без переусложнения.
- Decoupled `add_intent_qa` корректно покрывает три реальных interview-сценария (`1 ответ → N правок`, `1 ответ → 0 правок`, `0 ответов → 1 правка`), которые first-cut вариант с `*_from_interview` покрывал криво.
- Edit-tools одинаковы для всех режимов (interview / light_work / new_project) — у сервера нет режимного состояния, и agent instruction остаётся единственным местом, где этот режим живёт.
- Минимальный агентский surface: агент видит только `text` Intent/Instruction, не нагружается обучающим материалом (qa/review/версии). Меньше шума в контексте → меньше токенов → меньше сбоев на длинных сессиях.
- Убран `reason?` из edit-tools → меньше параметров и одно правило: «зачем» уже есть в `mcp_call_log` и `intent_qa`, второй раз нет смысла.
- Серверный `get_instruction_bundle(mode, intent_id?)` снимает класс ошибок «агент забыл подмешать common».
- `read_*_text` с `truncated` + `next_start_line` даёт агенту надёжный способ читать большие документы без угадывания лимитов.
- Жёсткий запрет `replace_by_line_range` / `full_replace` ставит барьер на класс операций, разрушающих контекст больших документов.

### Negative / Risks

- Связь qa ↔ конкретная правка теряется: восстанавливается только по timestamp. Если в анализе понадобится точная привязка, придётся либо хранить явный `qa_id` в version-документе, либо вводить ту же `from_interview`-сцепку, которую сейчас отвергли. Оценим по dogfooding-данным.
- Агент может забыть `add_intent_qa` — серверной валидации нет. Митигируется инструкцией и dogfooding-телеметрией (ADR-0004), но в первые сессии часть q/a может теряться.
- Жёсткий байт-точный матч `old_text` чувствителен к whitespace и BOM. Агенту придётся аккуратно копировать фрагменты, иначе будут `match_not_found`. Альтернативы (нормализация whitespace) отвергнуты, потому что они приводят к скрытым «не такая правка применилась».
- `add_intent_qa` / `add_intent_review` с `expected_version` могут вынудить агента перечитывать Intent чаще, чем хочется, если параллельно идёт правка. Для MVP solo-first риск низкий; пересмотрим, если станет проблемой.
- Серверный лимит чтения 64 000 символов — эвристика. Может оказаться мал для очень больших Intent'ов или велик для маленьких моделей. Будет уточнён по dogfooding-данным; формального обязательства держать его именно таким нет.

## Amendment — slash-command surface через MCP prompts

Slash-команды — договорённость в agent instruction/prompt; backend не парсит их как отдельную доменную сущность. В MVP эта договорённость приземляется на встроенный в MCP примитив **prompts** (`prompts/list` + `prompts/get`) того же сервера `Throne.Api`, а не на копируемый markdown в чужой агент. Это даёт три выгоды: атомарное обновление prompts вместе с tools, рендеринг аргументов клиентом (Claude Code, Cursor) как нативных slash-команд `/mcp__throne__<name>`, и audit-видимость (см. amendment к ADR-0004).

Контракт MVP:

- 4 prompts: `tinterview`, `twork`, `tnew`, `treview`.
- Аргументы у каждого: `intent_id?: string`, `text?: string` (snake_case как у tools, по тому же `pragma CA1707`).
- Mode mapping для `get_instruction_bundle` зашит в каждом prompt'е и не настраивается клиентом: `tinterview→interview`, `twork→light_work`, `tnew→new_project`, `treview→light_work`.
- Prompts-методы возвращают строку (один user-`PromptMessage`): общий блок правил (active-resolution, optimistic concurrency, edit discipline, error catalogue, запреты) + per-command playbook + подстановка переданных аргументов.
- Реализация — `Throne.Api.Mcp.Prompts.IntentPrompts` с `[McpServerPromptType]` / `[McpServerPrompt]`, регистрация через симметричный `AddThronePrompt<T>()` helper рядом с `AddThroneTool<T>()`.

Зависимостей модели и tools'ов это не меняет: 9 MVP-tools §1 остаются единственным write-API.
