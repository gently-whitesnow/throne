# Throne — Agent Brief

## 1. Рабочее название

`Throne`

Система описывается как рабочее облако, в котором хранится работа пользователя. Пользователь использует эту сохранённую работу для следующих работ, а сам процесс взаимодействия с AI должен постепенно улучшаться на основе предыдущих intent’ов, вопросов, ответов и review-замечаний.

В MVP улучшение системы не реализуется как отдельная сложная логика. Главная цель первой версии — задогфудить сам процесс и начать собирать данные, на которых следующая итерация сможет улучшать interview/work.

## 2. Суть продукта

Приложение хранит рабочие единицы пользователя и позволяет взаимодействовать с ними через MCP-интерфейс.

Базовая единица работы — `Intent`.

`Intent` — это минимальная формализованная единица намерения пользователя. Через него система может:

* понять, что пользователь хочет изменить, исследовать или создать;
* доуточнить намерение через `interview`;
* запустить работу через `work`;
* сохранить вопросы, ответы и review-замечания как данные о местах, где AI не понял пользователя, задачу или предпочтения.

## 3. Минимальный MVP

MVP должен быть достаточно маленьким, чтобы пользователь мог начать развивать саму систему через dogfooding.

Стартовые действия:

1. `interview` — позволяет уточнять и редактировать `Intent.text`.
2. `work` — позволяет по `Intent` выполнить полезную работу агентом.

В MVP не требуется:

* полноценная CRM;
* проектный менеджмент;
* статусы Intent;
* UI;
* отдельный `WorkRun`;
* удаление/архивация Intent и Instruction;
* сложная логика обучения;
* автоматическое улучшение инструкций.

## 4. Центральная гипотеза

Если всю работу пользователя представить как поток `Intent`’ов, а в каждом Intent сохранять не только итоговый текст, но и места непонимания AI, то система сможет накопить материал для последующего улучшения interview/work.

В MVP ценность не в том, что система уже “обучается”, а в том, что она начинает собирать правильный материал для будущего обучения:

* какие вопросы AI задавал;
* какие ответы дал пользователь;
* как после этого изменился Intent;
* какие ошибки AI допустил во время work;
* какие замечания пользователь оставил на review.

## 5. Взаимодействие с системой

Основной интерфейс взаимодействия — MCP.

Пользовательский dogfooding UX строится через slash-команды. Slash-команды — это не отдельная backend-сущность, а договорённость в agent instruction/prompt. Агент получает команду от пользователя, интерпретирует её и вызывает нужные MCP tools.

Минимальные slash-команды MVP:

```text
/tinterview [intentId?] [text?]     // создать или продолжить interview
/twork [intentId?] [text?]          // light work по Intent
/tnew [intentId?] [text?]           // new project work по Intent
/treview [intentId?] <замечание>    // добавить review-замечание и продолжить исправление
```

Все команды имеют префикс `t`, чтобы их было проще отличать от обычных команд окружения/агента.

Старые имена `/interview`, `/lw`, `/np`, `/review` в MVP не используются.

Backend/MCP-сервер не обязан парсить slash-команды. Он предоставляет tools, а slash-команды живут на уровне агента.

Основной технический интерфейс — MCP.

### 5.1. Active Intent resolution

Агент должен поддерживать понятие текущего Intent в рамках своей conversation/session.

Правило выбора Intent:

1. Если команда содержит явный `intentId`, агент работает с этим Intent и делает его текущим для сессии.
2. Если команда не содержит `intentId`, но в текущей сессии уже есть active Intent, агент работает с ним.
3. Если команда не содержит `intentId` и active Intent отсутствует, агент создаёт новый Intent из текста команды/сообщения пользователя.
4. Если текста недостаточно для создания осмысленного Intent, агент задаёт один уточняющий вопрос.

Это правило применяется ко всем slash-командам:

```text
/tinterview
/twork
/tnew
/treview
```

Следствие: пользователь может начать не только с `/tinterview`, но и сразу с `/twork`, `/tnew` или `/treview`. В таком случае агент создаёт Intent сам, если не может привязаться к существующему.

Примеры:

```text
/tinterview хочу сделать MCP-хранилище intent'ов
```

Создаёт новый Intent и начинает interview.

```text
/twork
```

Если в сессии уже есть active Intent — запускает light work по нему.

```text
/twork intent_123
```

Продолжает работу по существующему Intent и делает его active Intent.

```text
/treview не надо было создавать отдельный сервис, достаточно расширить модуль
```

Если active Intent есть — добавляет review к нему и идёт исправлять результат. Если active Intent отсутствует — создаёт новый Intent из review-контекста, фиксирует замечание и дальше действует по нему.

Система должна предоставлять агенту инструменты, через которые он может:

* создавать `Intent`;
* читать `Intent` (canonical `text` — без qa/review/истории версий);
* редактировать `Intent.text` через file-like операции (точная замена, вставка после строки);
* добавлять пары вопрос/ответ как отдельную операцию (не сцепляя с правкой `text`);
* добавлять review-замечания;
* читать инструкции из облака пакетом для текущего режима.

Инструкции в MVP редактируются пользователем напрямую (mongosh, в будущем — HTTP-эндпойнт `Throne.Api`). Агентского write-API для инструкций нет (см. ADR-0003 §1).

UI в первом MVP не продумывается.

## 6. Хранилище

Хранилище MVP: `MongoDB + MCP`.

MVP — solo-first. В первой версии нет `workspace_id`, `created_by`, ролей, прав доступа и командной модели.

Это сознательное ограничение: система должна быстрее дойти до dogfooding, а не сразу становиться multi-user продуктом.

MongoDB является основным canonical storage.

Важное требование: `Intent.text` и `Instruction.text` должны версионироваться.

Версионирование нужно потому, что агент может мутировать документы через MCP, но должна оставаться история изменений.

Версии отделены от основного чтения объекта: `get_intent` и `get_instruction` возвращают метаданные и текущий номер версии. Полный текст возвращается только если явно передан `include_text = true`; история читается отдельными tools.

## 7. Модель Intent

Canonical-документ компактен: только `text`, `current_version`, `tags`, timestamps. `qa[]` и `review[]` физически живут в отдельных коллекциях (`intent_qa`, `intent_review`) как training-only данные и **не** возвращаются агенту ни в одной read-операции (см. ADR-0002 §2/§5).

```text
Intent (collection: intents — canonical state)
- id
- text                // главный редактируемый документ
- current_version     // optimistic concurrency, ≥ 1
- tags[]              // проекты/темы
- created_at
- updated_at
```

История версий `Intent.text` живёт в единой коллекции `text_versions` (общая для Intent и Instruction, см. §7.2 и ADR-0002 §4) и через MCP агенту не выставляется — её читают только ETL-скрипты для будущего обучения системы.

`get_intent(intent_id)` всегда возвращает полный `text` вместе с `current_version` и `tags`. Для больших документов агент использует `read_intent_text` диапазонами; серверный лимит ответа — 64 000 символов (ADR-0003 §2).

### 7.1. Intent.text

`Intent.text` — главный документ Intent’а.

Во время `interview` агент редактирует именно `text`, а не отдельный `Intent Spec`.

Это осознанное упрощение MVP: один Intent = один основной текст + служебные массивы `qa` и `review`.

`Intent.text` может быть большим документом. В MVP не нужно искусственно ограничивать его размер маленьким лимитом вроде 20 000 символов.

Ключевое требование: агент должен работать с `Intent.text` почти как с локальным markdown-файлом:

* получить полный текст на старте `interview` или `work`, если это нужно для контекста;
* перечитать конкретный диапазон строк;
* найти фрагмент по текстовому поиску;
* точечно заменить найденный фрагмент;
* сохранить новую ревизию текста.

Агент не должен пересылать весь большой документ при каждом небольшом изменении, если достаточно точечной правки.

### 7.2. text_versions (единая коллекция)

История `Intent.text` и `Instruction.text` хранится в **единой** коллекции `text_versions` с дискриминатором `owner_kind ∈ { intent | instruction }`. Формат — delta-only после v1: первая версия хранит полный текст, последующие — только параметры конкретной правки. Это даёт ~140× экономию хранилища относительно full-snapshot формата на типичной dogfooding-сессии (см. ADR-0002 §4).

```text
TextVersion (collection: text_versions)
- id
- owner_kind         // intent | instruction
- owner_id
- version            // ≥ 1
- kind               // create | replace | insert
- snapshot           // только для kind = create
- old_text           // только для kind = replace
- new_text           // только для kind = replace
- after_line         // только для kind = insert
- insert_text        // только для kind = insert
- changed_at
- changed_by         // user | agent | system
```

Полей `previous_version` и `reason` нет: порядок восстанавливается по `version`, «зачем сделана правка» выводится из `mcp_call_log` (ADR-0004) и `intent_qa` для interview-сессий.

Правила записи:

- `create_intent` → `version = 1`, `kind = create`, `snapshot = <начальный текст>`.
- Каждая успешная `replace_intent_text` → новая версия `kind = replace` с `old_text` / `new_text` ровно как пришли в tool.
- Каждая успешная `insert_intent_text_after_line` → новая версия `kind = insert` с `after_line` / `insert_text`.
- Запись версии — серверная операция, агент в `text_versions` не пишет напрямую.
- Запись версии и инкремент `current_version` атомарны (детали транзакционности — storage ADR следующего шага, см. ADR-0002 §6).

Восстановление произвольной версии — O(N) replay (v1 snapshot + последовательное применение delta) и в MVP MCP read API **не** выставляется. Это материал для аудита и ETL.

Canonical current state остаётся в `Intent.text` / `Instruction.text` основного документа.

Ручное изменение `Intent.text` не порождает запись в `intent_qa`. Запись в `intent_qa` создаётся только отдельным tool `add_intent_qa` (см. §7.3 и §9.3).

### 7.3. intent_qa (отдельная коллекция, training-only)

`intent_qa` хранит вопросы и ответы, появившиеся во время interview. Это материал для будущего обучения системы; агенту он в рантайме не нужен и через MCP read API **не** возвращается (ADR-0002 §5, ADR-0003 §1).

```text
IntentQa (collection: intent_qa)
- id
- intent_id
- intent_version_at_write    // current_version Intent в момент записи
- question
- answer
- created_at
- created_by                 // обычно agent
```

Запись создаётся только отдельным tool:

```text
add_intent_qa(intent_id, expected_version, question, answer) -> Ack
```

`add_intent_qa` **не** инкрементирует `current_version` (qa — не правка text), но проверяет `expected_version`, чтобы запись прикреплялась к известному состоянию текста. `intent_version_at_write` фиксируется сервером.

Связь qa ↔ конкретная правка text **не** хранится явной ссылкой и восстанавливается по timestamp при анализе. Это сознательное упрощение MVP: см. ADR-0003 §5 и альтернативу №1 там же — отвергнутые `*_from_interview` варианты ломались на сценариях «1 ответ → N правок» и «1 ответ → 0 правок».

### 7.4. Intent.tags

`tags[]` нужны для простой фильтрации Intent’ов по проектам и темам.

`source` в MVP не используется: все Intent’ы создаются через slash-команды/агента.

При создании Intent агент должен передавать `tags[]`, если они понятны из пользовательского запроса или текущего рабочего контекста.

Если теги не переданы явно, агент должен попытаться определить имя текущего репозитория/рабочей директории и записать его первым тегом.

Пример:

```text
tags: ["throne"]
```

Это не означает, что `Throne` хранит связь `Intent` → repo. Репозиторий остаётся execution context агента, а тег — только удобная метка для поиска и группировки.

Если агент не может уверенно определить репозиторий/проект, `tags[]` может быть пустым.

### 7.5. intent_review (отдельная коллекция, training-only)

`intent_review` хранит замечания пользователя после выполнения work. Как и `intent_qa`, это training-only данные: через MCP read API агенту не выставляются.

```text
IntentReview (collection: intent_review)
- id
- intent_id
- intent_version_at_write
- note                  // замечание
- reason                // контент: почему это важно / что AI понял неправильно
- created_at
- created_by            // обычно agent от имени пользователя
```

Запись создаётся отдельным tool:

```text
add_intent_review(intent_id, expected_version, note, reason) -> Ack
```

Запись review **не** инкрементирует `current_version`, но `expected_version` обязателен (ADR-0002 §6).

Назначение: сохранить данные о том, где AI сделал не то, не так понял задачу или нарушил предпочтения пользователя. В MVP review не порождает автоматическую память и не улучшает инструкции автоматически — это материал для следующей итерации продукта.

## 8. Модель Instruction

`Instruction` — отдельный объект облака. В MVP редактируется **только** пользователем напрямую (mongosh / будущий HTTP-эндпойнт). Агентского write-API через MCP нет (см. ADR-0003 §1, альтернатива №7).

При первом запуске система должна создать минимальные seed-инструкции по умолчанию для каждого `kind`:

```text
common
interview
light_work
new_project
```

Seed-инструкции нужны, чтобы агент сразу мог работать через `get_instruction_bundle(mode)`, даже если пользователь ещё не создал собственные инструкции.

Seed-инструкции должны быть короткими. Это не “большой system prompt”, а стартовый минимальный набор правил для dogfooding.

Инструкция используется для инициализации interview/work.

Минимальная структура:

```text
Instruction (collection: instructions)
- id
- kind                // common | interview | light_work | new_project
- text
- current_version     // optimistic concurrency, ≥ 1
- created_at
- updated_at
```

Агент видит инструкции только через `get_instruction_bundle(mode)` — пакетом для текущего режима, целиком (см. §14 и ADR-0003 §7). Отдельных read-tools `get_instruction(id)` / `read_instruction_text` / `search_instruction_text` в MVP нет: bundle отдаёт seed-инструкции целиком, они короткие, range-read и поиск избыточны.

История версий `Instruction.text` живёт в той же коллекции `text_versions` с `owner_kind = instruction` (см. §7.2).

### 8.1. Instruction.kind

`kind` нужен, чтобы агент мог быстро выбрать релевантные инструкции для конкретного режима работы.

Допустимые значения MVP:

```text
common       // общие инструкции, применяются ко всем режимам
interview    // инструкции для уточнения Intent
light_work   // инструкции для маленьких задач
new_project  // инструкции для создания/развития нового проекта
```

При запуске режима агент получает:

```text
interview:   common + interview
/lw:         common + light_work
/np:         common + new_project
```

В MVP не используем `tags[]`, чтобы не усложнять модель.

### 8.2. Instruction.text

`Instruction.text` редактируется в MVP **только** пользователем напрямую (mongosh / будущий HTTP). Через MCP агент инструкции не правит.

Версионируется по тем же правилам, что и `Intent.text` (см. §7.2): записи живут в общей коллекции `text_versions` с `owner_kind = instruction`, формат delta-only после v1.

### 8.3. Instruction.text_versions

Отдельной коллекции под Instruction-версии нет — используется общая `text_versions` (см. §7.2). Поля те же; `owner_kind = instruction`, `owner_id = <instruction_id>`.

### 8.4. Seed-инструкции

Минимальные seed-инструкции MVP:

```text
Instruction(kind: common)
Работай минималистично. Не усложняй модель без необходимости. Предпочитай dogfooding completeness over product completeness. Если есть неопределённость, явно зафиксируй её и задай следующий полезный вопрос.
```

```text
Instruction(kind: interview)
Задавай по одному вопросу. После ответа пользователя обновляй Intent.text через MCP и сохраняй question/answer в Intent.qa. Не создавай отдельный spec-документ: редактируй только Intent.text.
```

```text
Instruction(kind: light_work)
Работай в текущем репозитории/рабочей директории агента. Используй Intent.text как основную задачу. Не создавай лишние сущности и не сохраняй результат work в Intent.
```

```text
Instruction(kind: new_project)
Работай в текущем репозитории/рабочей директории агента. Используй Intent.text как постановку для нового проекта. Создай минимальный рабочий скелет, достаточный для следующей итерации dogfooding.
```

## 9. Interview

`interview` — действие, которое помогает пользователю превратить сырой Intent в более пригодный для работы Intent.

Команды:

```text
/tinterview [intentId?] [text?]
```

### 9.1. /tinterview с новым текстом

Если команда содержит новый текст и не содержит `intentId`, агент создаёт новый `Intent` с исходным `text`, делает его active Intent и начинает интервьюировать пользователя.

Во время интервью агент:

* задаёт вопросы;
* получает ответы;
* редактирует `Intent.text` через MCP;
* сохраняет каждую пару вопрос/ответ в `Intent.qa`;
* сохраняет новую версию `Intent.text`.

### 9.2. /tinterview по существующему Intent

Если команда содержит `intentId`, агент продолжает interview по существующему Intent и делает его active Intent.

Если `intentId` не указан, но в сессии уже есть active Intent, агент продолжает interview по нему.

Он читает текущий `Intent.text`, существующий `qa`, затем задаёт следующий полезный вопрос или предлагает закончить interview.

### 9.3. MCP edit во время interview

При изменении Intent агент отправляет в MCP не новый полный текст, а file-like правку: `replace_intent_text` (точная замена) либо `insert_intent_text_after_line` (вставка после строки). Edit-tools одинаковы для всех режимов и о режиме (interview / light_work / new_project) не знают — режим существует только в agent instruction.

Запись пары вопрос/ответ — отдельный decoupled tool `add_intent_qa(intent_id, expected_version, question, answer)`. Этот выбор покрывает три реальных interview-сценария: «1 ответ → N правок», «1 ответ → 0 правок», «0 ответов → 1 правка» (см. ADR-0003 §1, альтернатива №1; от прежних `*_from_interview` сцепок отказались).

В рамках одного interview-шага агент по seed-инструкции `Instruction(kind: interview)` сначала вызывает `add_intent_qa`, затем выполняет одну или несколько правок `Intent.text`. Серверной валидации «правка только при свежем qa» нет — режим серверу неизвестен (ADR-0003 §5). Соблюдение порядка — задача agent instruction; контроль — через dogfooding-телеметрию `mcp_call_log` (ADR-0004 §8).

`replace_intent_text(intent_id, expected_version, old_text, new_text) -> Intent`:

1. проверяет `expected_version == current_version`;
2. требует, чтобы `old_text` встречался в текущем `text` ровно один раз (byte-exact: whitespace, переносы, BOM значимы);
3. атомарно обновляет `text`, инкрементирует `current_version`, пишет delta-запись в `text_versions` (`kind = replace`).

`insert_intent_text_after_line(intent_id, expected_version, after_line, insert_text) -> Intent`:

- `after_line = 0` — вставка в начало документа;
- `after_line` валиден на диапазоне `0 .. total_lines` текущей версии;
- атомарно обновляет `text`, инкрементирует `current_version`, пишет delta-запись `kind = insert`.

При нарушениях сервер возвращает typed `ApiException` с actionable detail (`intent.version_conflict`, `intent.text.match_not_found`, `intent.text.match_ambiguous`, `intent.text.line_out_of_range`). Контракт ошибок и поля detail — в ADR-0003 §3/§4.

`replace_by_line_range` и `full_replace` в MVP не вводятся. Line-range используется для чтения, но не для записи: номера строк дрейфуют между версиями, а точная замена по `old_text` безопаснее и ближе к агентскому редактированию локальных файлов. Полная перезапись большого документа возможна только косвенно — через `replace_intent_text` с `old_text == текущий весь текст`.

## 10. Work

`work` — действие, которое запускает выполнение по Intent.

В MVP отдельного `WorkRun` нет.

Результат `work` не хранится внутри `Intent`. Результат живёт в репозитории или другом внешнем рабочем контексте, где агент выполнял задачу. В облаке сохраняется только то, что нужно для улучшения процесса: исходный `Intent.text`, interview-след `qa[]` и review-замечания `review[]`.

В MVP нет отдельного approval/status-механизма. `work` может запускаться по любому `Intent`; готовность Intent определяется пользователем и агентом в процессе работы, а не отдельным полем модели.

Видятся два типа работы:

### 10.1. Light work

Команда:

```text
/twork [intentId?] [text?]
```

Назначение: маленькие задачи.

Если `intentId` указан, агент использует этот Intent и делает его active Intent.

Если `intentId` не указан, но active Intent уже есть, агент использует active Intent.

Если `intentId` не указан и active Intent отсутствует, агент создаёт новый Intent из текста команды/сообщения пользователя и сразу запускает work по нему.

Целевой репозиторий/рабочая директория определяется текущим контекстом агента. `Throne` не хранит связь `Intent` → repo и не управляет execution context.

Агенту прокидываются:

* `Intent.text`;
* релевантные инструкции из облака для маленьких задач.

### 10.2. New project

Команда:

```text
/tnew [intentId?] [text?]
```

Назначение: создание или развитие нового проекта.

Если `intentId` указан, агент использует этот Intent и делает его active Intent.

Если `intentId` не указан, но active Intent уже есть, агент использует active Intent.

Если `intentId` не указан и active Intent отсутствует, агент создаёт новый Intent из текста команды/сообщения пользователя и сразу запускает new project work по нему.

Целевой репозиторий/рабочая директория определяется текущим контекстом агента. `Throne` не хранит связь `Intent` → repo и не управляет execution context.

Агенту прокидываются:

* `Intent.text`;
* более богатые инструкции из облака: стек, архитектурные предпочтения, правила разработки.

## 11. Review после work

После `work` пользователь ревьюит результат в репозитории.

Если AI сделал что-то не так, пользователь добавляет замечание через slash-команду:

```text
/treview [intentId?] <замечание>
```

`/treview` — это не только запись feedback. Это continuation-команда: агент должен сохранить замечание в `Intent.review`, внести в `Intent.text` нужные детали, если они улучшают постановку, и затем вернуться к исправлению результата в текущем репозитории.

### 11.1. Как выбирается Intent для /treview

Правило такое же, как для остальных команд:

1. Если указан `intentId` — добавить review к этому Intent и сделать его active Intent.
2. Если `intentId` не указан, но в сессии есть active Intent — добавить review к нему.
3. Если `intentId` не указан и active Intent отсутствует — создать новый Intent из review-контекста, добавить review и продолжить работу по нему.

Это позволяет пользователю начать с review даже в новой сессии.

Пример:

```text
/treview не надо было создавать отдельный сервис, достаточно расширить существующий модуль
```

Если active Intent отсутствует, агент должен создать Intent примерно с таким смыслом:

```text
Исправить предыдущую реализацию: не создавать отдельный сервис для маленькой фичи, а расширить существующий модуль.
```

После этого агент добавляет review item и работает с текущим репозиторием.

### 11.2. MCP tool для review

```text
add_intent_review(intent_id, expected_version, note, reason) -> Ack
```

`expected_version` обязателен (ADR-0002 §6). Запись review **не** инкрементирует `current_version`, но проверяет его, чтобы review прикреплялся к известному состоянию `text`. Сервер фиксирует `intent_version_at_write` в `intent_review`. `Ack` = `{ intent_id, current_version, accepted: true }`.

В MVP review — это не отдельный workflow approval. Это способ сохранить места, где AI не понял пользователя или задачу.

### 11.3. Review loop

После `/treview` агент должен:

1. определить или создать Intent;
2. добавить запись в `Intent.review`;
3. при необходимости точечно обновить `Intent.text`, чтобы замечание стало частью постановки;
4. перечитать релевантные инструкции через `get_instruction_bundle(light_work)`;
5. продолжить исправление в текущем репозитории/рабочей директории;
6. не сохранять результат work в Intent.

Если замечание неполное и агент не может безопасно действовать, он задаёт один уточняющий вопрос.

## 12. Dogfooding-критерий MVP

Система считается достаточно готовой для первого dogfooding, если пользователь может:

1. Создать `Intent` через MCP.
2. Запустить `/tinterview <text>`.
3. Во время interview получить обновления `Intent.text`.
4. Сохранить вопросы и ответы в `intent_qa` через `add_intent_qa`.
5. Продолжить interview через `/tinterview <intentId>`.
6. Запустить `/twork [intentId?]` или `/tnew [intentId?]` без отдельного approval/status шага.
7. Получить результат работы в репозитории, без сохранения результата work внутри `Intent`.
8. После review добавить замечания в `intent_review` через `/treview [intentId?] <замечание>`.
9. Редактировать `Intent.text` через агента (file-like edit-tools) или вручную (mongosh).
10. Редактировать `Instruction.text` вручную (mongosh / будущий HTTP). Агентского write-API для инструкций в MVP нет.

История версий `Intent.text` / `Instruction.text` пишется в `text_versions` (см. §7.2), но через MCP агенту не выставляется — это материал для аудита и ETL.

## 13. Принципы MVP

* Минимум сущностей.
* `Intent` — центральная единица.
* `Instruction` — отдельная единица инициализации поведения агента.
* MCP-first.
* MongoDB как canonical storage.
* UI не нужен в первой версии.
* `WorkRun` не нужен в первой версии.
* Результат `work` не хранится в `Intent`; источник результата — репозиторий.
* Целевой репозиторий/рабочая директория определяется текущим контекстом агента, а не моделью `Intent`.
* `Intent.text` — главный редактируемый документ.
* `intent_qa` фиксирует непонимание на interview (training-only, агенту невидим).
* `intent_review` фиксирует непонимание на work/review (training-only, агенту невидим).
* `Intent.text` и `Instruction.text` версионируются в общей коллекции `text_versions`, формат delta-only после v1.
* Каждый MCP-вызов попадает в append-only `mcp_call_log` (ADR-0004) — основа dogfooding-телеметрии.
* Улучшение системы — следующая итерация, MVP только собирает данные.
* Dogfooding важнее полноты.
* Система должна быть объяснима и быстро пересоздаваема.
* В MVP ничего не удаляется и не архивируется, чтобы не терять dogfooding-данные.

## 14. Минимальный набор MCP tools

MVP-набор — **9 tools**, ровно столько, сколько нужно для slash-команд `/tinterview`, `/twork`, `/tnew`, `/treview` и dogfooding-критериев §13. Финальный контракт зафиксирован в [ADR-0003 §1](specs/ADR/0003-mcp-text-editing-semantics.md).

Чтение (4):

```text
get_intent(intent_id) -> IntentWithText
read_intent_text(intent_id, start_line? = 1, line_count?, max_chars?) -> TextSlice
search_intent_text(intent_id, query, context_lines? = 3, limit? = 10) -> TextSearchResult[]
get_instruction_bundle(mode) -> InstructionWithText[]
```

Запись Intent (5):

```text
create_intent(text, tags?) -> Intent
replace_intent_text(intent_id, expected_version, old_text, new_text) -> Intent
insert_intent_text_after_line(intent_id, expected_version, after_line, insert_text) -> Intent
add_intent_qa(intent_id, expected_version, question, answer) -> Ack
add_intent_review(intent_id, expected_version, note, reason) -> Ack
```

`Ack = { intent_id, current_version, accepted: true }`. qa/review-документы агенту не возвращаются.

Сознательно **не вводятся** в MVP:

- `list_intents`, `list_instructions`, `list_*_versions` — ни один dogfooding-сценарий §13 их не задействует.
- `get_instruction(id)`, `read_instruction_text`, `search_instruction_text` — агент берёт инструкции только пакетом через `get_instruction_bundle(mode)`.
- Все write-tools для Instruction (`create_instruction`, `replace_instruction_text`, `insert_instruction_text_after_line`) — инструкции в MVP правит пользователь напрямую (mongosh / будущий HTTP).
- `*_from_interview` варианты edit-tools — связь qa ↔ правка организована через decoupled `add_intent_qa` (ADR-0003 §1, альтернатива №1).
- `replace_by_line_range`, `full_replace` — оба разрушают контекст больших документов или провоцируют гонки на дрейфующих номерах строк.
- Параметр `include_text?` у `get_intent` — `get_intent` всегда возвращает полный `text`.
- Параметр `reason?` у edit-tools — «зачем» уже есть в `mcp_call_log` и `intent_qa`; `reason` остаётся только в `add_intent_review` как контентное поле.
- Параметр `response_format` (см. §15.3).

Когда любой из этих tools понадобится — отдельный ADR.

### 14.1. Text editing semantics

Агент работает с `Intent.text` через file-like интерфейс: read range, search, exact string replace, insert after line. Универсального `patch` tool нет.

`read_intent_text(intent_id, start_line?, line_count?, max_chars?)`:

- line numbers 1-indexed; `start_line` по умолчанию = 1;
- если `line_count` не передан — сервер возвращает с конца документа `total_lines - start_line + 1` строк под серверным лимитом;
- серверный жёсткий лимит ответа = **64 000 символов** (ADR-0003 §2). `max_chars` — клиентский потолок, не выше серверного;
- ответ:
  ```text
  TextSlice
  - current_version
  - start_line
  - end_line
  - total_lines
  - content
  - truncated         // true если ответ обрезан по max_chars
  - next_start_line   // подсказка для пагинации, если truncated
  ```

`search_intent_text(intent_id, query, context_lines? = 3, limit? = 10)`:

- поиск case-sensitive, по подстроке (regex в MVP не вводится);
- серверный max `limit` = 50;
- ответ — массив `TextSearchResult { match_line, match_column, context, context_start_line }`;
- если общее число совпадений > `limit`, в ответе поле `total_matches_estimate` и hint «уточни запрос».

`replace_intent_text(intent_id, expected_version, old_text, new_text)` и `insert_intent_text_after_line(intent_id, expected_version, after_line, insert_text)` — контракт см. в §9.3 и ADR-0003 §3/§4.

Actionable error codes (единый реестр `Throne.Application.ErrorCodes`, отдаются через Problem Details writer):

| Код | Когда |
|---|---|
| `intent.version_conflict` | `expected_version != current_version` |
| `intent.text.match_not_found` | `old_text` не найден |
| `intent.text.match_ambiguous` | `old_text` найден >1 раза |
| `intent.text.line_out_of_range` | `after_line` вне `0..total_lines` |

Для инструкций отдельных read-tools нет — агент получает их пакетом:

```text
get_instruction_bundle(interview)    -> [common, interview]
get_instruction_bundle(light_work)   -> [common, light_work]
get_instruction_bundle(new_project)  -> [common, new_project]

/tinterview -> get_instruction_bundle(interview)
/twork      -> get_instruction_bundle(light_work)
/tnew       -> get_instruction_bundle(new_project)
/treview    -> get_instruction_bundle(light_work)
```

Маппинг режимов жёстко зашит на сервере. Если для нужного `kind` нет ни одной инструкции (что не должно случаться благодаря seed bootstrap, §15.1), сервер возвращает то, что есть, и явный flag `missing_kinds[]`.

Это снижает риск, что агент забудет `common`-инструкции или соберёт неправильный набор.

## 15. Закрытые технические решения

### 15.1. Bootstrap seed-инструкций

Seed-инструкции создаются idempotent bootstrap-логикой при старте приложения.

Правило:

* если инструкция нужного `kind` уже есть — не перезаписывать её;
* если инструкции нужного `kind` нет — создать seed-инструкцию;
* bootstrap не должен ломать пользовательские правки;
* отдельная ручная команда bootstrap для MVP не нужна.

### 15.2. current_version

`Intent` и `Instruction` хранят отдельное поле `current_version` в основном документе. Канонические структуры — в §7 (Intent) и §8 (Instruction).

Правило:

* при создании объекта `current_version = 1`;
* при каждой успешной правке `text` `current_version += 1`;
* `replace_intent_text` / `insert_intent_text_after_line` / `add_intent_qa` / `add_intent_review` требуют `expected_version`;
* `add_intent_qa` / `add_intent_review` проверяют `expected_version`, но **не** инкрементируют `current_version` (qa/review — не правка text);
* инкремент `current_version`, обновление `text` и запись delta в `text_versions` атомарны;
* при `expected_version != current_version` сервер возвращает typed `ApiException` с кодом `intent.version_conflict` и detail-полями, чтобы агент мог перечитать документ и повторить.

### 15.3. response_format

Отдельный параметр `response_format: concise | detailed` в MVP не добавляется. Вместо этого фиксированное поведение:

* `get_intent` всегда возвращает полный `text` с `current_version` и `tags`;
* `read_intent_text` используется для чтения большого `text` диапазонами под серверным лимитом 64 000 символов;
* `search_intent_text` возвращает компактные результаты с ограниченным контекстом;
* `get_instruction_bundle(mode)` возвращает массив `InstructionWithText` целиком.

Это проще для агента и уменьшает количество режимов поведения tools.

### 15.4. MCP call audit log

Каждый MCP-вызов попадает в append-only коллекцию `mcp_call_log` (`tool_name`, `arguments`, `intent_id`, `session_id`, `outcome`, `error_code`, `result_summary`, `duration_ms`, `server_version`). Запись делает middleware на границе `Throne.Api` через порт `IMcpCallLogSink` — best-effort, без блокировки tool-вызова при сбое sink. Покрытие гарантируется конструкцией: единый registration-helper, architecture-тест, startup fail-fast, параметризованный smoke integration-тест. Подробности — [ADR-0004](specs/ADR/0004-mcp-call-audit-log.md). Это обоснование центральной гипотезы §4 — без журнала «улучшение системы — следующая итерация» теряет материал.

## 16. Что отдавать агенту для реализации

Документ считается достаточным для первой реализации MVP в связке с ADR-0001..0004 ([REGISTRY](specs/ADR/REGISTRY.md)). Архитектурные решения — там, продуктовая постановка — здесь.

Следующий шаг — превратить его в implementation prompt для coding agent:

1. поднять backend на .NET 10 + MongoDB в существующем `apps/api` (clean architecture, см. ADR-0001);
2. реализовать доменные модели `Intent`, `Instruction` и единую `TextVersion` (`owner_kind` + delta-only после v1, см. ADR-0002);
3. реализовать коллекции `intent_qa`, `intent_review` (training-only, без MCP read API);
4. реализовать idempotent bootstrap seed-инструкций при старте;
5. реализовать 9 MCP tools из §14 через единый registration-helper (см. ADR-0003 и ADR-0004 §4);
6. реализовать file-like text editing semantics (`replace` / `insert_after_line`) с actionable error codes (см. §14.1);
7. реализовать `mcp_call_log` middleware + best-effort `IMcpCallLogSink` (ADR-0004), с гарантией покрытия by construction;
8. добавить тесты: версионирование, exact replace (match_not_found / match_ambiguous), conflict handling, seed bootstrap, audit log coverage (параметризованный по реестру tools);
9. подготовить agent instruction для slash-команд `/tinterview`, `/twork`, `/tnew`, `/treview` с маппингом на `get_instruction_bundle(mode)`.

Вне рамок первой реализации:

* UI;
* multi-user/workspace;
* WorkRun;
* хранение результата work;
* автоматическое обучение;
* удаление/архивация;
* retrieval/semantic search;
* сложная permission model;
* MCP write-tools для Instruction;
* MCP read-tools для истории версий, qa, review, audit log;
* PII-маскирование в `mcp_call_log.arguments`.

## 17. Backlog для следующего агента

После первого вертикального среза (см. §16) реализованы 3 MCP tools — `create_intent`, `get_intent`, `read_intent_text` — через `[McpServerTool]`-классы + `AuditingMcpServerTool` decorator + `AddThroneTool<T>` registration-helper (ADR-0003 + ADR-0004). Дальше — два независимых задания. Можно брать любое; задание B логически опирается на A, но если A не сделано, в задании B можно временно обойтись sequential writes без транзакции и пометить TODO.

### 17.1. Шаг A — Mongo replica set + multi-document transactions

**Цель.** Закрыть требование ADR-0002 §6: «инкремент `current_version` + update `text` + insert в `text_versions` атомарны». Сейчас `MongoIntentRepository.CreateAsync` делает два последовательных `InsertOne` без транзакции (это безопасно только потому, что у `create_intent` нет concurrency). Любая будущая write-операция (`replace_intent_text`, `insert_intent_text_after_line`, `add_intent_qa`, `add_intent_review`) требует настоящей транзакции — иначе сбой между двумя `InsertOne` оставит canonical state в несогласованном виде.

**Что делать.**

1. Поднять Mongo replica set в Testcontainers.
   - `Testcontainers.MongoDb 4.1.0` поддерживает `MongoDbBuilder().WithReplicaSet()` — использовать его в `MongoFixture`.
   - Проверить, что существующие интеграционные тесты (`MongoIntentRepositoryTests`, `MongoMcpCallLogSinkTests`) продолжают проходить.
2. В `Throne.Infrastructure` ввести абстракцию для работы с транзакциями:
   - Порт `IMongoUnitOfWork` (или `ITransactionScope`) в `Throne.Application/Ports`, реализация `MongoUnitOfWork` в `Throne.Infrastructure/Mongo`. Интерфейс должен скрывать `IClientSessionHandle` от Application слоя (он не должен знать про MongoDB.Driver).
   - Альтернатива: passthrough через `Func<CancellationToken, Task>` лямбду — `await uow.ExecuteAsync(async ct => { ... }, ct)`. Внутри `MongoUnitOfWork.ExecuteAsync` стартует session, открывает transaction, проходит лямбду, коммитит / откатывает.
3. Переписать `MongoIntentRepository.CreateAsync` через UoW: оба `InsertOne` идут с `IClientSessionHandle` в одной транзакции.
4. Документировать в production setup, что Mongo должен быть replica set (не standalone). Для локальной разработки — либо docker-compose с `--replSet`, либо `mongod --replSet rs0` + `rs.initiate()`. Записать в `apps/api/README.md` или в `MongoOptions` xml-doc.
5. Если транзакции стоят дорого / усложняют локальный setup, рассмотреть альтернативу: денормализовать current_version и last text version-id в самом Intent-документе (single-document update — атомарен в Mongo by default). Тогда `text_versions` пишется ВТОРЫМ; если этот write упадёт, последующая read-операция увидит canonical state с current_version=N, но в `text_versions` будет дырка. История перестаёт быть strong-consistent. Это нарушает ADR-0002 §4 — закрыть отдельным amendment к ADR-0002 или **не выбирать этот путь**.

**Acceptance criteria.**

- `bash scripts/quality/verify.sh` зелёный.
- Новый интеграционный тест: эмулировать сбой между двумя записями (например, infrastructure-тест, который оборачивает `IIntentRepository` в repo, бросающий после первого `InsertOne`) — проверить, что после rollback ни `intents`, ни `text_versions` не содержат частичных данных.
- `MongoIntentRepository.CreateAsync` использует только UoW; прямые вызовы `InsertOneAsync(doc, options: null, ct)` без session — запрещены (можно проверить через architecture-тест или review).

**Файлы (ориентир, не догма).**

- `apps/api/src/Throne.Application/Ports/IMongoUnitOfWork.cs` (новый).
- `apps/api/src/Throne.Infrastructure/Mongo/MongoUnitOfWork.cs` (новый).
- `apps/api/src/Throne.Infrastructure/Mongo/MongoIntentRepository.cs` (правка `CreateAsync`).
- `apps/api/src/Throne.Infrastructure/DependencyInjection.cs` (регистрация UoW).
- `apps/api/tests/Throne.Infrastructure.Tests/MongoFixture.cs` (replica set).
- `apps/api/tests/Throne.Infrastructure.Tests/Mongo/TransactionRollbackTests.cs` (новый).

### 17.2. Шаг B — `replace_intent_text` MCP tool

**Цель.** Закрыть основной write-сценарий `interview` / `light_work`: точечная замена куска `Intent.text` с optimistic concurrency. Контракт зафиксирован в ADR-0003 §3 и intent.md §9.3 + §14.1.

**Что делать.**

1. Расширить `Throne.Domain/Intents/Intent.cs`:
   - Метод `ReplaceText(string oldText, string newText, DateTimeOffset now)` — ищет ровно одно вхождение `oldText` в `Text`, заменяет на `newText`, инкрементирует `CurrentVersion`, обновляет `UpdatedAt`. Возвращает либо новую `TextVersion` (kind=replace, owner_kind=intent, version=N+1, old_text/new_text как пришли), либо результат с дискриминатором ошибки (`MatchNotFound` / `MatchAmbiguous`). Решение «exception vs Result»: в Domain бросать `DomainException` с типом ошибки, в Application мэппить в `ApiException(intent.text.match_not_found)` / `intent.text.match_ambiguous`. Альтернатива — Result tuple, см. USER.md «Старайся в нагруженных частях возвращать кортеж». Выбор за исполнителем; согласовать стиль с уже существующим `Intent.Create`.
2. Расширить `IIntentRepository`:
   - Добавить метод `ReplaceTextAsync(IntentId id, int expectedVersion, string oldText, string newText, DateTimeOffset now, CancellationToken ct)` ИЛИ обобщить через `UpdateAsync(Intent intent, TextVersion newVersion, CancellationToken ct)` с оптимистичной проверкой `current_version` в Mongo-уровне (UpdateOneAsync с фильтром `_id == id && current_version == expected`).
   - Возвращаемое значение: типизированный outcome (`Replaced` / `VersionConflict { current }` / `MatchNotFound` / `MatchAmbiguous`). Применить тот же паттерн, что в Domain.
3. Реализовать в Mongo (`MongoIntentRepository`):
   - Проверка `expected_version == current_version` через filter в `UpdateOneAsync` ИЛИ через явный read-modify-write внутри транзакции из шага A.
   - При успехе атомарно: update `intents` (новый `text`, `current_version + 1`, `updated_at`) + insert в `text_versions` (kind=replace, owner_kind=intent, version=N+1, old_text/new_text). Атомарность — через UoW из шага A. Если шаг A ещё не сделан, временно сделать sequential writes и пометить TODO.
   - Edge case: пустая строка `new_text` — допустима (удаление фрагмента).
4. Application use case `ReplaceIntentTextHandler`:
   - Команда: `ReplaceIntentTextCommand(intent_id, expected_version, old_text, new_text)`.
   - Возвращает обновлённый `Intent`.
   - Маппит outcome из repo в `ApiException` с правильным кодом (`intent.version_conflict` / `intent.text.match_not_found` / `intent.text.match_ambiguous`) и detail-полями из ADR-0003 §3.
5. MCP tool в `IntentTools`:
   - Метод `ReplaceIntentText(string intent_id, int expected_version, string old_text, string new_text, CancellationToken ct)` с `[McpServerTool(Name = "replace_intent_text", UseStructuredContent = true)]`.
   - Возвращает `Intent` (как `create_intent` / `get_intent`).
6. Audit log:
   - Decorator `AuditingMcpServerTool` уже знает про tool name и аргументы. Дополнительная работа не требуется — всё бесплатно.

**Acceptance criteria.**

- `bash scripts/quality/verify.sh` зелёный.
- Domain unit-тест: `ReplaceText` корректно работает на ровном вхождении; `MatchNotFound` / `MatchAmbiguous` возвращаются точно по ADR-0003 §3.
- Application unit-тест: маппинг outcome → ApiException + detail-поля.
- Infrastructure integration-тест: вторая запись (`text_versions`) появляется атомарно с обновлением `intents`; `expected_version` mismatch → `intent.version_conflict`; concurrent replace на одном Intent — один выигрывает, другой получает version_conflict.
- API integration-тест (или unit на уровне `IntentTools` через DI): tool в DI обёрнут в `AuditingMcpServerTool`, audit-запись с `tool_name = replace_intent_text` и `intent_id` появляется.
- Architecture-инвариант: ничего нового не нужно — `AddThroneTool<IntentTools>` уже автоматически подхватит новый метод по `[McpServerTool]`.

**Файлы (ориентир).**

- `apps/api/src/Throne.Domain/Intents/Intent.cs` (метод `ReplaceText`).
- `apps/api/src/Throne.Application/Ports/IIntentRepository.cs` (новый метод).
- `apps/api/src/Throne.Application/Intents/ReplaceIntentTextHandler.cs` (новый).
- `apps/api/src/Throne.Application/DependencyInjection.cs` (регистрация handler).
- `apps/api/src/Throne.Infrastructure/Mongo/MongoIntentRepository.cs` (реализация).
- `apps/api/src/Throne.Api/Mcp/Tools/IntentTools.cs` (новый метод с `[McpServerTool]`).
- Тесты: `Throne.Domain.Tests/Intents/IntentReplaceTextTests.cs`, `Throne.Application.Tests/Intents/ReplaceIntentTextHandlerTests.cs`, `Throne.Infrastructure.Tests/Mongo/MongoIntentReplaceTests.cs`.

**Что вне scope этого шага.**

- `insert_intent_text_after_line` — отдельный, аналогичный по структуре tool. Сделать тем же паттерном после `replace_intent_text` (или сразу пакетом, если время позволяет).
- `add_intent_qa` / `add_intent_review` — те же optimistic concurrency правила, но не инкрементируют `current_version` (см. ADR-0002 §6). Отдельный шаг.
- Search (`search_intent_text`) — read-only, без транзакций; отдельный шаг.
