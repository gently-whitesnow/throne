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
* читать `Intent`;
* редактировать `Intent.text`;
* добавлять вопросы и ответы в `Intent.qa`;
* добавлять review-замечания в `Intent.review`;
* читать инструкции из облака;
* редактировать инструкции вручную или через агента.

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

```text
Intent
- id
- text                // главный редактируемый текст intent'а
- current_version     // текущая версия text для optimistic concurrency
- qa[]                // вопросы и ответы interview
- review[]            // замечания пользователя после work/review
- tags[]              // проекты/темы, к которым относится intent
- created_at
- updated_at
```

Версии `Intent.text` не возвращаются вместе с `Intent` по умолчанию. История читается отдельным MCP tool. Полный `Intent.text` также не обязан возвращаться по умолчанию: для больших документов агент должен использовать `read_intent_text`.

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

### 7.2. Intent.text_versions

Каждое изменение `Intent.text` должно сохранять новую версию.

Минимальная структура версии:

```text
IntentTextVersion
- version
- text
- changed_at
- changed_by          // user | agent | system
- reason              // optional
```

Версия должна фиксировать факт изменения текста, но не обязана всегда хранить полную копию большого документа.

Для MVP допустимы два режима записи версии:

```text
full_snapshot  // полная копия текста, удобно для первой версии и простых изменений
patch          // точечное изменение: что было заменено и на что
```

Минимально важно хранить:

```text
IntentTextVersion
- version
- previous_version
- edit_type           // full_snapshot | exact_replace
- text_snapshot       // optional, для full_snapshot
- old_text            // optional, для exact_replace
- new_text            // optional, для exact_replace
- changed_at
- changed_by          // user | agent | system
- reason              // optional
```

Canonical current state остаётся в `Intent.text`. История нужна для аудита и понимания изменений, а не обязательно для сложного восстановления любой версии в MVP.

Ручное изменение `Intent.text` не добавляет запись в `qa[]`. Оно только обновляет `text` и добавляет новую запись в историю версий. `qa[]` остаётся чисто interview-следом.

### 7.3. Intent.qa

`qa` хранит вопросы и ответы, появившиеся во время interview.

Когда агент правит Intent в рамках interview, он должен передавать в MCP-запросе:

1. как поправить документ;
2. какой был вопрос;
3. какой был ответ.

Минимальная структура:

```text
IntentQA
- question
- answer
- created_at
```

Назначение `qa`: сохранить данные о том, чего AI изначально не понял и что пришлось уточнять у пользователя.

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

### 7.5. Intent.review

`review` хранит замечания пользователя после выполнения work.

Минимальная структура:

```text
IntentReviewItem
- note       // замечание
- reason     // причина, почему это важно / что AI понял неправильно
- created_at
```

Назначение `review`: сохранить данные о том, где AI сделал не то, не так понял задачу или нарушил предпочтения пользователя.

В MVP review не порождает отдельную автоматическую память и не улучшает инструкции автоматически. Это материал для следующей итерации продукта.

## 8. Модель Instruction

`Instruction` — отдельный объект облака.

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
Instruction
- id
- kind                // common | interview | light_work | new_project
- text
- current_version     // текущая версия text для optimistic concurrency
- created_at
- updated_at
```

Версии `Instruction.text` не возвращаются вместе с `Instruction` по умолчанию. История читается отдельным MCP tool. Полный `Instruction.text` также не обязан возвращаться по умолчанию: для больших документов агент должен использовать `read_instruction_text`.

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

`Instruction.text` можно редактировать руками и через агента.

Как и `Intent.text`, он должен версионироваться.

### 8.3. Instruction.text_versions

Минимальная структура версии:

```text
InstructionTextVersion
- version
- text
- changed_at
- changed_by          // user | agent | system
- reason              // optional
```

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

При изменении Intent агент должен отправлять в MCP не новый полный текст, а file-like правку.

Для MVP принимается str_replace-based модель редактирования:

```text
replace_intent_text_from_interview
- intent_id
- expected_version
- old_text
- new_text
- question
- answer
- reason              // optional
```

MCP-сервер должен:

1. проверить `expected_version`, чтобы агент не редактировал устаревший текст;
2. проверить, что `old_text` найден ровно один раз;
3. заменить `old_text` на `new_text`;
4. добавить новую запись в историю версий;
5. добавить новую запись в `Intent.qa`.

Если `old_text` не найден или найден несколько раз, сервер должен вернуть actionable error: почему правка не применена и что агенту сделать дальше. Например: перечитать диапазон строк, расширить `old_text` соседним контекстом или использовать `search_intent_text`.

Для добавления нового блока используется отдельный insert-tool:

```text
insert_intent_text_after_line_from_interview
- intent_id
- expected_version
- after_line           // 0 = вставка в начало документа
- insert_text
- question
- answer
- reason               // optional
```

`replace_by_line_range` в MVP не добавляется. Line-range используется для чтения, но не для записи: номера строк могут дрейфовать, а точная замена по строке безопаснее и ближе к агентскому редактированию локальных файлов.

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
AddIntentReview
- intent_id
- note
- reason
```

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
2. Запустить `/interview <text>`.
3. Во время interview получить обновления `Intent.text`.
4. Сохранить вопросы и ответы в `Intent.qa`.
5. Продолжить interview через `/interview <intentId>`.
6. Запустить `/twork [intentId?]` или `/tnew [intentId?]` без отдельного approval/status шага.
7. Получить результат работы в репозитории, без сохранения результата work внутри `Intent`.
8. После review добавить замечания в `Intent.review` через `/treview [intentId?] <замечание>`.
9. Редактировать `Intent.text` вручную или через агента.
10. Редактировать `Instruction.text` вручную или через агента.
11. Видеть историю версий `Intent.text` и `Instruction.text` через отдельные tools.

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
* `Intent.qa` фиксирует непонимание на interview.
* `Intent.review` фиксирует непонимание на work/review.
* `Intent.text` и `Instruction.text` версионируются.
* Улучшение системы — следующая итерация, MVP только собирает данные.
* Dogfooding важнее полноты.
* Система должна быть объяснима и быстро пересоздаваема.
* В MVP ничего не удаляется и не архивируется, чтобы не терять dogfooding-данные.

## 14. Минимальный набор MCP tools

Предварительно необходимы следующие MCP tools:

```text
create_intent(text, tags?) -> Intent
get_intent(intent_id, include_text?) -> Intent
read_intent_text(intent_id, start_line?, line_count?, max_chars?) -> TextSlice
search_intent_text(intent_id, query, context_lines?, limit?) -> TextSearchResult[]
list_intent_versions(intent_id, limit?, cursor?) -> IntentTextVersion[]
replace_intent_text(intent_id, expected_version, old_text, new_text, reason?) -> Intent
insert_intent_text_after_line(intent_id, expected_version, after_line, insert_text, reason?) -> Intent
replace_intent_text_from_interview(intent_id, expected_version, old_text, new_text, question, answer, reason?) -> Intent
insert_intent_text_after_line_from_interview(intent_id, expected_version, after_line, insert_text, question, answer, reason?) -> Intent
add_intent_review(intent_id, note, reason) -> Intent
list_intents(tags?, limit?, cursor?) -> Intent[]

create_instruction(kind, text) -> Instruction
get_instruction(instruction_id, include_text?) -> Instruction
read_instruction_text(instruction_id, start_line?, line_count?, max_chars?) -> TextSlice
search_instruction_text(instruction_id, query, context_lines?, limit?) -> TextSearchResult[]
list_instruction_versions(instruction_id, limit?, cursor?) -> InstructionTextVersion[]
replace_instruction_text(instruction_id, expected_version, old_text, new_text, reason?) -> Instruction
insert_instruction_text_after_line(instruction_id, expected_version, after_line, insert_text, reason?) -> Instruction
list_instructions(kind?, limit?, cursor?) -> Instruction[]
get_instruction_bundle(mode) -> Instruction[]
```

### 14.1. Text editing tools

`Intent.text` и `Instruction.text` редактируются через file-like интерфейс.

Решение MVP: не делать универсальный `patch` tool. Вместо этого дать агенту минимальный набор, похожий на работу с локальными файлами:

```text
read/view range
search
exact string replace
insert after line
```

Чтение:

```text
read_*_text(id, start_line?, line_count?, max_chars?)
```

Правила чтения:

* line numbers — 1-indexed;
* `start_line` по умолчанию = 1;
* если `line_count` не передан, сервер может вернуть весь документ, но обязан применить `max_chars`/дефолтную защиту от слишком большого ответа;
* ответ должен содержать `current_version`, `start_line`, `end_line`, `total_lines`, `content`, `truncated`.

Поиск:

```text
search_*_text(id, query, context_lines?, limit?)
```

Правила поиска:

* возвращать найденные фрагменты с номерами строк и соседним контекстом;
* дефолт `context_lines = 3`;
* дефолт `limit = 10`;
* если совпадений слишком много, вернуть первые результаты и подсказку сузить запрос.

Редактирование точной заменой:

```text
replace_*_text(id, expected_version, old_text, new_text, reason?)
```

Правила `replace`:

* `expected_version` обязателен;
* `old_text` должен совпасть ровно один раз;
* whitespace и переносы строк значимы;
* если совпадение не найдено или найдено несколько раз, правка не применяется;
* ошибка должна быть actionable: предложить `search`/`read range` и объяснить, как уточнить `old_text`.

Редактирование вставкой:

```text
insert_*_text_after_line(id, expected_version, after_line, insert_text, reason?)
```

Правила `insert`:

* `after_line = 0` означает вставку в начало документа;
* `after_line` должен быть валидным для текущей версии документа;
* вставка создаёт новую версию текста.

`replace_by_line_range` в MVP не добавляется. Номера строк используются для навигации и вставки, но не для замены диапазона. Для замены блока агент должен сначала прочитать диапазон, затем использовать `replace_*_text` с точным `old_text`.

`full_replace` как обычный tool не добавляется. Полная перезапись слишком легко превращается в потерю контекста и хуже подходит для dogfooding больших документов.

Инструкции выбираются по `kind`.

Для каждого режима агент должен использовать `get_instruction_bundle(mode)`, чтобы не собирать bundle вручную.

```text
get_instruction_bundle(interview)    -> common + interview
get_instruction_bundle(light_work)   -> common + light_work
get_instruction_bundle(new_project)  -> common + new_project

/tinterview -> get_instruction_bundle(interview)
/twork      -> get_instruction_bundle(light_work)
/tnew       -> get_instruction_bundle(new_project)
/treview    -> get_instruction_bundle(light_work)
```

Это снижает риск, что агент забудет `common`-инструкции или выберет неправильный набор.

## 15. Закрытые технические решения

### 15.1. Bootstrap seed-инструкций

Seed-инструкции создаются idempotent bootstrap-логикой при старте приложения.

Правило:

* если инструкция нужного `kind` уже есть — не перезаписывать её;
* если инструкции нужного `kind` нет — создать seed-инструкцию;
* bootstrap не должен ломать пользовательские правки;
* отдельная ручная команда bootstrap для MVP не нужна.

### 15.2. current_version

`Intent` и `Instruction` должны хранить отдельное поле `current_version`.

Причина: агент должен быстро получать версию для optimistic concurrency, не вычисляя её по истории.

Обновлённые структуры:

```text
Intent
- id
- text
- current_version
- qa[]
- review[]
- tags[]
- created_at
- updated_at
```

```text
Instruction
- id
- kind
- text
- current_version
- created_at
- updated_at
```

Правило:

* при создании объекта `current_version = 1`;
* при каждой успешной правке текста `current_version += 1`;
* edit tools требуют `expected_version`;
* если `expected_version != current_version`, сервер возвращает conflict error и просит агента перечитать документ.

### 15.3. response_format для list/search

Отдельный параметр `response_format: concise | detailed` в MVP не добавляется.

Вместо этого используется фиксированное поведение:

* `list_*` возвращает компактные summary-объекты без полного текста;
* `get_*` возвращает метаданные и полный текст только при `include_text = true`;
* `read_*_text` используется для чтения текста;
* `search_*_text` возвращает компактные результаты с ограниченным контекстом.

Это проще для агента и уменьшает количество режимов поведения tools.

## 16. Что отдавать агенту для реализации

Документ считается достаточным для первой реализации MVP.

Следующий шаг — превратить его в implementation prompt для coding agent:

1. создать backend с MongoDB;
2. реализовать модели `Intent`, `Instruction`, `IntentTextVersion`, `InstructionTextVersion`;
3. реализовать idempotent bootstrap seed-инструкций;
4. реализовать MCP tools из раздела 14;
5. реализовать file-like text editing semantics;
6. добавить тесты на версионирование, exact replace, conflict handling и seed bootstrap;
7. подготовить agent instruction для slash-команд `/tinterview`, `/twork`, `/tnew`, `/treview`.

Вне рамок первой реализации:

* UI;
* multi-user/workspace;
* WorkRun;
* хранение результата work;
* автоматическое обучение;
* удаление/архивация;
* retrieval/semantic search;
* сложная permission model.
