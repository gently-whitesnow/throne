Да, `/tdream` лучше ложится в дух проекта.

Не отдельный “модуль эволюции” с кучей сущностей, а **ночной сон системы**:

```text id="7k1dse"
сырьё → сон → 1–5 предложений → пользователь применяет/пропускает → сырьё считается обработанным
```

# Идея `/tdream`

`/tdream` — команда, которая берёт **новое необработанное сырьё** и превращает его в предложения по улучшению инструкций.

```text id="rwp0ss"
/tdream
```

Что делает агент:

1. находит необработанные `review`, `qa`, `outcome`, `mcp_call_log`, future `work_delta`;
2. собирает из них краткую картину;
3. предлагает небольшие правки к `Instruction.text`;
4. пользователь принимает, редактирует или пропускает;
5. использованное сырьё больше не участвует в следующих `/tdream`.

---

# Сильно упрощённая модель

Я бы оставил **одну новую коллекцию**:

```text id="5uyj9n"
dream_runs
```

Без отдельных:

```text id="16mx38"
LearningSignal
EvolutionRun
InstructionProposal
InstructionProposalDecision
```

Всё это можно вложить внутрь одного `DreamRun`.

---

# `DreamRun`

```text id="5exse6"
DreamRun
- id
- status                 // pending | closed
- evidence_refs[]        // какое сырьё было обработано
- proposals[]            // предложенные правки инструкций
- created_at
- closed_at?
```

## `evidence_refs[]`

Ссылки на использованное сырьё:

```text id="nyyeyf"
EvidenceRef
- kind                   // review | qa | outcome | mcp_call | work_delta | verification
- id
```

Пример:

```json id="kwjx6c"
[
  { "kind": "review", "id": "review_123" },
  { "kind": "outcome", "id": "outcome_55" },
  { "kind": "mcp_call", "id": "call_991" }
]
```

Смысл:

> Эти куски опыта уже были просмотрены в рамках dream-run.

---

## `proposals[]`

Встроенные предложения:

```text id="fz451z"
DreamProposal
- id
- target_kind            // common | interview | light_work | new_project
- problem
- proposed_rule
- evidence_summary
- decision               // pending | applied | skipped | edited
- final_rule?
- applied_instruction_version?
```

Пример:

```text id="a5xjx8"
target_kind:
light_work

problem:
Агент переоценил масштаб маленькой задачи и создал отдельный сервис.

proposed_rule:
Перед созданием нового сервиса, модуля или абстракции сначала проверь, можно ли решить задачу расширением существующего модуля. Если оба варианта подходят и Intent явно не требует новой границы, выбирай меньшее изменение.

decision:
pending
```

---

# Как понимать “необработанное сырьё”

Я бы **не добавлял `processed_at` в каждую сырьевую коллекцию**.

Почему:

* `mcp_call_log` лучше оставить append-only;
* `intent_review`, `intent_qa`, `outcome` тоже лучше не мутировать ради служебного процесса;
* одна и та же запись может потом участвовать в другом типе анализа.

Проще:

```text id="yuakoh"
Сырьё считается обработанным, если его ref уже есть в closed DreamRun.evidence_refs.
```

То есть `/tdream` ищет:

```text id="e8ngeg"
все review/qa/outcome/logs,
которые ещё не входят в закрытые dream_runs
```

---

# Важный нюанс: когда помечать обработанным

Не сразу после генерации proposals.

Иначе будет так:

```text id="mcbu2b"
/tdream сгенерировал предложения
пользователь закрыл чат
сырьё помечено обработанным
предложения потерялись
```

Лучше так:

```text id="nzfmsx"
DreamRun.status = pending
```

Пока pending — сырьё ещё не считается окончательно обработанным.

После того как пользователь сказал:

```text id="d0xj9y"
применить / пропустить / закрыть сон
```

ставим:

```text id="lx13pb"
DreamRun.status = closed
```

И только closed dream-runs исключаются из будущих `/tdream`.

---

# UX команды

## Основная команда

```text id="hwv3h5"
/tdream
```

Поведение:

```text id="xxvw0w"
Собери всё новое сырьё с прошлого закрытого сна и предложи улучшения инструкций.
```

## Возможный вывод

```text id="g9wdvf"
Нашёл новое сырьё:
- 3 review-замечания
- 2 accepted outcomes
- 14 MCP-вызовов get_instruction_bundle / work tools

Предлагаю 2 правки инструкций.
```

Дальше:

```text id="t85r93"
1. light_work

Проблема:
В двух задачах агент создавал новую архитектурную сущность там, где пользователь ожидал локальное изменение.

Правка:
Перед созданием нового сервиса, модуля или abstraction сначала проверь, можно ли решить задачу расширением существующего модуля.

Evidence:
- review_123: "не надо было создавать отдельный сервис..."
- review_148: "слишком большой скоуп для такой правки"

Решение:
apply / edit / skip
```

---

# Нужно ли несколько команд?

Можно оставить **одну `/tdream`** и сделать всё диалогом.

```text id="ip7yir"
/tdream
```

Агент показывает предложения.

Пользователь отвечает обычным текстом:

```text id="2swmux"
первое применить, второе пропустить
```

или:

```text id="a191gd"
первое перепиши мягче: не запрещай сервисы, а проси сначала проверить существующий модуль
```

То есть не нужны:

```text id="j10216"
/tapply-dream
/tskip-dream
/tedit-dream
```

Для MVP это лишнее.

---

# Минимальные MCP tools

Для `/tdream` достаточно 3 tools.

```text id="mv742w"
run_dream() -> DreamRun
apply_dream_proposal(dream_run_id, proposal_id, final_rule?) -> DreamRun
close_dream_run(dream_run_id) -> DreamRun
```

## `run_dream`

```text id="w9bmcb"
run_dream()
```

Делает:

1. находит необработанное сырьё;
2. создаёт `DreamRun(status = pending)`;
3. генерирует proposals;
4. возвращает proposals агенту.

## `apply_dream_proposal`

```text id="n2czp5"
apply_dream_proposal(dream_run_id, proposal_id, final_rule?)
```

Делает:

1. берёт proposal;
2. если `final_rule` передан — применяет отредактированную версию;
3. добавляет правило в нужную инструкцию;
4. создаёт новую `text_versions` запись для `Instruction`;
5. помечает proposal как `applied`;
6. записывает `applied_instruction_version`.

## `close_dream_run`

```text id="sjseua"
close_dream_run(dream_run_id)
```

Делает:

1. все pending proposals помечает как `skipped`;
2. ставит `DreamRun.status = closed`;
3. с этого момента `evidence_refs[]` считается обработанным сырьём.

---

# Куда применять правки

Для простоты — append-only в секцию:

```markdown id="jhkf28"
## Learned rules
```

Например `Instruction(kind: light_work)`:

```markdown id="6zn4fb"
## Learned rules

- Перед созданием нового сервиса, модуля или abstraction сначала проверь, можно ли решить задачу расширением существующего модуля. Если оба варианта подходят и Intent явно не требует новой границы, выбирай меньшее изменение.
```

Если секции нет — создать её в конце инструкции.

Это проще, чем полноценный patch/diff.

---

# Почему `/tdream`, а не `/tevolve`

`/tevolve` звучит инженерно и системно.

`/tdream` звучит как продуктовая метафора:

```text id="yfvmxh"
система прожила день → накопила опыт → во сне переработала → утром предложила новые правила
```

Это хорошо подходит к твоей идее рабочего облака, которое постепенно становится умнее.

---

# Как не сделать мусорную инструкцию

У `/tdream` должны быть жёсткие правила.

## 1. Максимум 5 proposals за запуск

```text id="r4zo5p"
Если сырья много — выбрать самые сильные сигналы.
```

Лучше 1 хорошая правка, чем 12 шумных.

## 2. Только маленькие правила

Proposal не должен переписывать всю инструкцию.

Плохо:

```text id="9o5yu5"
Полностью измени подход к light_work.
```

Хорошо:

```text id="mlyy3d"
Перед созданием новой архитектурной сущности сначала проверь возможность локального изменения.
```

## 3. Не создавать абсолютные правила

Плохо:

```text id="xl2nlg"
Никогда не создавай новые сервисы.
```

Хорошо:

```text id="dd19ql"
Если Intent явно не требует новой границы, предпочитай расширение существующего модуля.
```

## 4. Не применять без evidence

Каждый proposal должен ссылаться минимум на один `evidence_ref`.

## 5. Не применять без пользователя

`run_dream` только предлагает.

Инструкция меняется только через `apply_dream_proposal`.

---

# Как выбирать сырьё

Для первого варианта я бы брал:

```text id="284lz5"
1. intent_review
2. intent_outcome
3. get_instruction_bundle usages из mcp_call_log
4. verification failures, если добавишь
5. work_delta_summary, если добавишь
```

`intent_qa` можно пока брать осторожно. Там много шума, и не каждый вопрос/ответ означает проблему инструкции.

Приоритет:

```text id="oml3eq"
review > failed verification > manual correction > outcome > qa > raw mcp log
```

---

# Что делать с positive outcomes

`/tdream` должен видеть не только ошибки.

Если есть несколько `/taccept`, он может предложить закрепить успешный паттерн.

Пример:

```text id="a7ydj9"
Evidence:
- 3 accepted light_work задачи
- все были сделаны маленьким vertical slice
- без новых сущностей

Proposal:
Для light_work предпочитай один законченный вертикальный срез вместо широкого частичного изменения нескольких подсистем.
```

Это важно, иначе dream будет превращать агента только в осторожного бюрократа.

---

# Самая компактная версия спецификации

```markdown id="r3ulj6"
## /tdream

`/tdream` — команда переработки накопленного dogfooding-сырья в предложения по улучшению инструкций.

Команда не выполняет work по Intent. Она анализирует необработанные review/outcome/qa/log-сигналы и создаёт `DreamRun` с предложениями правок к `Instruction.text`.

Сырьё считается необработанным, если его reference ещё не входит в `evidence_refs[]` закрытого `DreamRun`.

`run_dream()` создаёт `DreamRun(status = pending)` и возвращает до 5 предложений.

Пользователь может принять, отредактировать или пропустить каждое предложение. Принятое предложение применяется только через `apply_dream_proposal`, который добавляет правило в секцию `## Learned rules` целевой инструкции и создаёт новую версию `Instruction.text`.

После завершения пользователь закрывает dream-run. `close_dream_run()` помечает оставшиеся pending proposals как skipped и переводит `DreamRun` в `closed`. Только после этого использованное сырьё считается обработанным и не участвует в следующих `/tdream`.

Инварианты:

- `/tdream` не меняет инструкции автоматически.
- Каждое предложение должно иметь evidence_refs.
- Один запуск создаёт максимум 5 proposals.
- Правки добавляются как маленькие условные правила, а не как полный rewrite инструкции.
- Raw evidence не мутируется; обработанность выводится из closed `DreamRun.evidence_refs`.
```

---

# Мой выбор для MVP+

Сделал бы так:

```text id="knzqyw"
1 новая коллекция:
- dream_runs

3 MCP tools:
- run_dream
- apply_dream_proposal
- close_dream_run

1 slash-команда:
- /tdream
```

Это достаточно просто, но уже замыкает цикл:

```text id="ukgym7"
Intent → Work → Review/Accept → Dream → Better instructions
```

Вопрос, который надо решить: `/tdream` должен обрабатывать **всё новое сырьё глобально** или только сырьё по **текущему active Intent**?
