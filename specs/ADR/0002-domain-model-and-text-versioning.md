# ADR-0002: Доменная модель Intent/Instruction и версионирование текста

## Status

Accepted (amended дважды: (1) qa/review вынесены в отдельные коллекции `intent_qa` / `intent_review`, версионирование унифицировано в `text_versions`, `reason` убран; (2) формат версий перепроектирован с full-snapshot на delta-only после v1, чтобы закрыть риск ~20 MB на Intent при долгом dogfooding; см. §2 / §4 / §5)

**Update 2026-06-21:** retired by [ADR-0043](0043-static-operational-skills-and-mcp-removal.md) в части MCP. Упоминания MCP-агента/MCP tools ниже историчны — MCP-поверхность удалена, доставку операций ведут статические CLI-скиллы. Доменная модель и версионирование текста из этого ADR в силе.

## Context

[ADR-0001](0001-foundation-clean-architecture-monorepo.md) зафиксировал слои и tech stack, но оставил доменную модель и контракт версионирования открытыми. [readme.md](../../readme.md) задаёт миссию и границы MVP: Intent/Instruction, версионирование `text`, training-only следы и dogfooding-телеметрию. Эти решения нужно зафиксировать **до** Mongo-схемы и до MCP tools — иначе любой первый vertical slice их закопает в коде без явного обоснования.

Throne MVP — single bounded context: `Intent` и `Instruction` тесно связаны через MCP-агента и solo-first сценарий. Modular-monolith правила из common seed-инструкции пока не активируются (нет второго bounded context).

Открытые на этом шаге вопросы и рассмотренные альтернативы:

1. **Где хранить qa/review.** Альтернативы: (a) embedded массивы в `Intent`, (b) отдельные коллекции `intent_qa` / `intent_review`, (c) единая коллекция `intent_feedback` с дискриминатором. Embedded раздувает canonical-документ и заставляет каждый `get_intent` тащить обучающий материал, который агенту не нужен (агент работает только с `text`). Единая коллекция — компромисс с nullable-полями. Выбрано (b): qa и review семантически разные сущности, общая схема стоит дороже, чем дублирование коллекции. Агент qa/review не читает вообще — только пишет.
2. **Где хранить text-версии.** Альтернативы: (a) embedded массив внутри Intent, (b) две параллельные коллекции `intent_text_versions` / `instruction_text_versions`, (c) одна коллекция `text_versions` с дискриминатором `owner_kind`. Embedded ограничивает 16 MB BSON и заставляет читать всю историю при каждом get. Между (b) и (c): логика версионирования у Intent и Instruction идентична («был такой текст в такой-то момент») — параллельные коллекции дублируют код без выгоды. Выбрано (c).
3. **Гранулярность version-документа.** Альтернативы: (a) delta-only после v1 snapshot, (b) full snapshot на каждую версию, (c) не хранить `text_versions` совсем, опираться только на `mcp_call_log`. Вариант (b) даёт O(1) restore, но при dogfooding объёме (текст ~100 KB × 200 правок ≈ **20 MB на Intent**) хранилище раздувается на порядки относительно ценности данных. Вариант (c) убирает дублирование, но конвертирует strong-consistent историю в best-effort (`mcp_call_log` пишется вне доменной транзакции — см. ADR-0004 §5), что для центральных обучающих данных Throne — регрессия. Выбран (a): v1 хранит полный текст, v2+ — только параметры конкретной правки (`old_text`/`new_text` или `after_line`/`insert_text`) с дискриминатором `kind`. Замер на той же сессии: 100 KB initial + ~200 байт × 200 правок ≈ **140 KB на Intent**, ~140× меньше. Restore O(N) допустим: история нужна для аудита и ETL, не для горячих сценариев. Бага «снапшот ⊥ дельта» нет по построению — снапшот один на v1, дальше только дельты, replay детерминирован.
4. **Формат конкурентного конфликта.** Альтернативы: thrown `ConflictException` vs typed `ApiException(code = "intent.version_conflict")`. Common seed-инструкция требует единый `ApiException` + реестр кодов и единый writer Problem Details. Выбран `ApiException`.
5. **Восстановимость произвольной версии.** Альтернативы: гарантировать восстановление любой версии vs история «для аудита, не для восстановления» в MVP. Границы MVP в readme фиксируют историю как материал для анализа, а не runtime read API. С delta-форматом из альтернативы #3 восстановление возможно через O(N) replay (v1 snapshot + последовательное применение delta 2..N), но это не обязательство — gap'ы в истории при сбое допустимы.
6. **Метаданные `reason` на write-операциях.** Альтернативы: (a) принимать `reason?` в edit-tools и хранить в version-документе, (b) не принимать. «Зачем агент это сделал» уже выводимо из `mcp_call_log` (session_id + tool_name + arguments по timestamp) и из `intent_qa` (q/a над тем же intent в interview). Выбрано (b): дублировать незачем. `reason` как **контентное** поле остаётся только в `IntentReviewItem.reason` — там оно описывает суть замечания, а не зачем оно записано.

7. **Periodic snapshot в delta-схеме.** Альтернативы: (a) только v1 snapshot, дальше всегда delta; (b) каждые K правок писать полный snapshot для O(K) restore. Вариант (b) ограничивает worst-case restore, но усложняет write-логику и schema. История читается редко (аудит, ETL для обучения), O(N) restore при N≈200 — это сотни мс на чтение, не пользовательский путь. Выбрано (a). Если dogfooding покажет, что N стабильно > 1000 на горячих Intent'ах — отдельный ADR введёт периодический checkpoint; сейчас это premature.

8. **Snapshot-fallback при большой delta.** Альтернативы: (a) если `|old_text| + |new_text| > 1.5 × |current_text|`, писать snapshot вместо delta; (b) всегда delta независимо от размера. Edge-case «delta больше snapshot» теоретически возможен (агент перезаписал почти весь документ через `replace`), но в реальном dogfooding редок: большие перезаписи — это `create_intent` или последовательность мелких правок, а не одна огромная `replace`. Выбрано (b): прозрачнее правило (одно условие на запись версии вместо двух), edge-case при необходимости закрывается отдельным ADR.

## Decision

### 1. Aggregate boundaries

Два независимых aggregate root в одном bounded context: `Intent` и `Instruction`. Никаких ссылок между ними на доменном уровне (связь — только через агентскую логику в `Throne.Application`). Modular-monolith разделение пока не вводится.

### 2. Поля `Intent`

```text
Intent (canonical document, collection: intents)
- id                  : string (Mongo ObjectId или ULID, см. ADR следующего шага по storage)
- text                : string                  // главный редактируемый документ
- current_version     : int   ≥ 1               // optimistic concurrency
- tags[]              : string                  // embedded
- created_at          : timestamp (UTC)
- updated_at          : timestamp (UTC)
```

`qa[]` и `review[]` — **не** embedded. Они живут в отдельных коллекциях и **не** возвращаются агенту в read-операциях (см. §5). Это обучающий материал, агенту он не нужен; canonical-документ остаётся компактным и не смешивает «текущее состояние» с «материалом для будущего обучения».

`tags[]` — простые строки. `source` поля нет: все Intent'ы MVP создаются через slash-команды/агента.

### 3. Поля `Instruction`

```text
Instruction (canonical document, collection: instructions)
- id              : string
- scope           : enum { user }   ; system-части manifest-managed, в Mongo не живут (см. [ADR-0014](0014-mcp-initialize-instructions-routing.md))
- user_id         : string?         ; обязателен для scope=user, MVP — "mvp-user"
- kind            : enum { common | interview | work | dream | fix }
- text            : string          ; пустая строка допустима для незаполненных user-антагонистов
- current_version : int ≥ 1
- created_at      : timestamp (UTC)
- updated_at      : timestamp (UTC)
```

`kind` хранится строкой в snake_case (см. common seed-инструкцию: enum-like значения на wire передаются строками в одном формате).

### 4. Версионирование текста

Версии живут в **единой** коллекции `text_versions` — одна и та же схема для Intent и Instruction, с дискриминатором `owner_kind`. Canonical current state — поле `text` в основном документе (`intents` / `instructions`). История нужна для аудита и будущего обучения, **не** для гарантированного быстрого восстановления произвольной версии.

Формат записи — delta-only после v1: первая версия хранит полный текст, последующие — только параметры конкретной правки.

```text
TextVersion (collection: text_versions)
- id              : string
- owner_kind      : enum { intent | instruction }
- owner_id        : string
- version         : int  ≥ 1
- kind            : enum { create | replace | insert }
- snapshot        : string?       // ровно для kind = create
- old_text        : string?       // ровно для kind = replace
- new_text        : string?       // ровно для kind = replace
- after_line      : int?          // ровно для kind = insert
- insert_text     : string?       // ровно для kind = insert
- changed_at      : timestamp (UTC)
- changed_by      : enum { user | agent | system }
```

Полей `previous_version` и `reason` **нет**. Порядок версий восстанавливается по полю `version`; «зачем сделана правка» — в `mcp_call_log` (см. ADR-0004) и в `intent_qa` для interview-сессий.

Правила:

- При `create_intent` / `create_instruction` пишется первая версия `version = 1`, `kind = create`, `snapshot = <начальный текст>`. Полей `old_text` / `new_text` / `after_line` / `insert_text` нет.
- Каждая успешная `replace_*_text` пишет `version = current_version + 1`, `kind = replace`, `old_text` / `new_text` ровно как пришли в tool. `snapshot` не записывается.
- Каждая успешная `insert_*_text_after_line` пишет `version = current_version + 1`, `kind = insert`, `after_line` / `insert_text` ровно как пришли в tool. `snapshot` не записывается.
- Запись версии — внутренняя серверная операция, агент `text_versions` не пишет напрямую.
- Версия неизменяема после записи. Сжатие/удаление истории — out of scope MVP.

Восстановление текста на версию N (для аудита/ETL):

- Загрузить версию 1 (`kind = create` со `snapshot`) — стартовое состояние.
- Применить версии `2..N` по порядку: `replace` (заменить `old_text` → `new_text` ровно один раз) или `insert` (вставить `insert_text` после строки `after_line`).
- Replay детерминирован: каждая delta была валидна против известного состояния в момент записи.

Восстановление **не** входит в MVP MCP read API. ETL-скрипты для обучения системы читают `text_versions` напрямую из Mongo.

### 5. qa и review (training-only data)

qa и review хранятся в **отдельных** коллекциях `intent_qa` и `intent_review`. Они **никогда** не возвращаются агенту ни в одной read-операции MCP API (см. ADR-0003 §1). Это сознательное ограничение: qa/review — материал для будущего обучения системы, агенту он не нужен в рантайме и только раздувал бы агентский контекст.

```text
IntentQa (collection: intent_qa)
- id                  : string
- intent_id           : string
- intent_version_at_write : int    // current_version Intent в момент записи (то, что давало expected_version)
- question            : string
- answer              : string
- created_at          : timestamp (UTC)
- created_by          : enum { user | agent | system }   // обычно agent
```

```text
IntentReview (collection: intent_review)
- id                  : string
- intent_id           : string
- intent_version_at_write : int
- note                : string
- reason              : string                            // контент: «почему это важно / что AI понял неправильно»
- created_at          : timestamp (UTC)
- created_by          : enum { user | agent | system }   // обычно agent от имени пользователя
```

`intent_version_at_write` фиксирует, к какому состоянию text относится qa/review — именно это раньше проверял `expected_version`. Поле важно для аналитики: при анализе обучающих данных оно позволяет точно сопоставить qa/review с конкретным состоянием Intent.

Read API для qa/review в MVP отсутствует. Чтение — напрямую из Mongo ETL-скриптами для обучения системы, как и `mcp_call_log`.

### 6. Optimistic concurrency

`current_version` хранится в основном документе (`intents` / `instructions`) и инкрементируется атомарно с записью version-документа в `text_versions`.

Контракт write-tools:

- Все `replace_*` / `insert_*` / `add_intent_qa` / `add_intent_review` принимают `expected_version`.
- Сервер принимает изменение только если `expected_version == current_version` на момент операции. Иначе — typed `ApiException` с кодом `intent.version_conflict` (или `instruction.version_conflict`) и detail-полями `current_version`, `expected_version`, чтобы агент мог перечитать документ и повторить.
- `add_intent_qa` / `add_intent_review` **не** инкрементируют `current_version` (qa и review — не правка text), но проверяют его. Это гарантирует, что обучающая запись прикреплена к известной версии текста (см. `intent_version_at_write` в §5).
- Транзакционность для text-правки: «инкремент current_version + update text + insert в text_versions» обеспечивается единой Mongo-операцией. Конкретный механизм (multi-document transaction vs single-document update) — деталь storage ADR следующего шага, домен от него не зависит.
- Транзакционность для qa/review: `expected_version`-проверка + insert в `intent_qa` / `intent_review` с `intent_version_at_write = current_version`.

Ошибки нормализуются через единый `ApiException` + реестр кодов в `Throne.Application` (см. common seed-инструкцию).

### 7. Канонический current state

`Intent.text` / `Instruction.text` в основном документе — единственный источник истины для чтения. История версий — append-only audit log. Чтение конкретной версии может быть реализовано позже отдельным MCP tool (тривиально благодаря полному снапшоту), но в MVP `read_*_text` всегда читает canonical state.

## Consequences

### Positive

- Canonical Intent компактен: только `text`, `current_version`, `tags`, timestamps. Чтение Intent не тянет обучающий материал, который агенту не нужен.
- Унифицированный `text_versions` для Intent и Instruction = одна реализация, одна форма индексов, одна логика записи. Меньше дублирования кода и тестов.
- Delta-only после v1 даёт ~140× экономию хранилища относительно full-snapshot формата (140 KB вместо 20 MB на типичную dogfooding-сессию). Risk «20 MB per Intent» закрыт by design.
- Точные параметры правки (`old_text` / `new_text` / `after_line` / `insert_text`) лежат в `text_versions` под strong-consistency (одна транзакция с обновлением canonical text), а не только в best-effort `mcp_call_log`. История обучающих данных не теряется при сбое audit sink.
- Replay детерминирован и доказуемо консистентен: каждая delta была валидна против известного состояния в момент записи, snapshot ⊥ delta-багов нет по построению (snapshot ровно один — на v1).
- qa/review в отдельных коллекциях с `intent_version_at_write` — обучающий материал точно привязан к состоянию text и легко выгружается ETL-скриптами без чтения canonical-документов.
- `expected_version` как обязательный параметр write-tools закрывает класс гонок «два агента одновременно правят один Intent» и одновременно гарантирует, что qa/review не приклеиваются к устаревшему состоянию.
- Контракт ошибок через `ApiException` + Problem Details согласован с common seed-инструкцией с первого реального write-эндпойнта.

### Negative / Risks

- Restore версии N стоит O(N) reads + replay. Для аудита и ETL приемлемо (история читается редко), но это нужно учитывать в любом будущем UI «show me text at version N» — там потребуется кеш или checkpoint-snapshot.
- В `text_versions` вернулся дискриминатор `kind` ({ create | replace | insert }) и набор optional полей под него. Schema чуть сложнее, чем «один blob `text`», но проще, чем full edit_type-граф из первой редакции (нет `previous_version`, `reason`, `text_snapshot`).
- Edge-case «delta больше snapshot» (агент перезаписал почти весь документ одним `replace`) теоретически возможен, snapshot-fallback сознательно не вводим. Если это станет шаблонной агентской практикой — закроем отдельным ADR.
- Replay полагается на целостность последовательности версий: пропуск одной версии в середине ломает восстановление всех последующих. Mongo durability + транзакционная запись (см. §6) этот риск закрывают; отдельных проверок целостности (например, hash-chain) не вводим.
- `current_version` в основном документе требует, чтобы любая запись версии и обновление основного документа были атомарны вместе. Это constraint на storage layer (multi-document transaction либо single-document update), и он будет конкретизирован отдельным storage ADR.
- qa/review недоступны через MCP read API. Если в будущей итерации система научится использовать прошлый interview-след для самообучения в рантайме, понадобится новый read-контракт — отдельный ADR.
