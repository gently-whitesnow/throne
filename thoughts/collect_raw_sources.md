Полезное сырьё делится на две категории:

```text
1. Evidence of failure — где агент ошибся.
2. Evidence of success — что сработало и должно повторяться.
```

Сейчас у тебя хорошо покрыто первое, но почти не покрыто второе.

# 1. Outcome-сигнал: чем закончилась работа

Сейчас `/treview` фиксирует негативный feedback. Но системе нужно знать и обратное: **когда работа была принята**.

## Полезное сырьё

```text
IntentOutcome
- intent_id
- outcome              // accepted | rejected | partially_accepted | abandoned
- user_note?
- created_at
```

Зачем:

* отличать “агент сделал хорошо” от “пользователь просто ушёл”;
* строить positive examples;
* понимать, какие instructions реально улучшают результат;
* позже делать eval dataset.

Минимальная команда:

```text
/taccept [intentId?] [note?]
```

Пример:

```text
/taccept хорошо, оставляем так
```

Это даст системе сигнал:

> Вот такой Intent + такие instructions + такой work-процесс привели к принятому результату.

Без этого у тебя learning loop будет перекошен в сторону ошибок.

---

# 2. Snapshot использованных инструкций

Очень важное сырьё: **какая именно версия инструкций была использована во время work/interview**.

Сейчас `get_instruction_bundle(mode)` возвращает инструкции, но для будущего анализа надо явно сохранять:

```text
InstructionBundleUse
- intent_id
- session_id
- mode
- instruction_refs[]
  - kind
  - instruction_id
  - version
- created_at
```

Почему это важно:

* review относится не просто к Intent, а к поведению агента под конкретными инструкциями;
* после изменения Instruction надо понимать, какие старые ошибки были до патча;
* можно сравнивать качество `light_work v3` vs `light_work v4`.

Можно не делать отдельную коллекцию: это частично уже есть в `mcp_call_log`, если `get_instruction_bundle` логирует `mode` и `result_summary` с `kind/version`.

Я бы усилил `result_summary` для `get_instruction_bundle`:

```text
result_summary:
- mode
- instructions:
  - kind
  - current_version
```

---

# 3. Code diff / touched files summary

Сейчас результат work живёт в репозитории и не хранится в Intent. Это правильно. Но для обучения полезен хотя бы **summary результата**.

Не полный код, а компактный след:

```text
WorkDeltaSummary
- intent_id
- session_id
- changed_files[]
- added_files[]
- deleted_files[]
- test_results?
- commands_run[]
- git_diff_stat?
- created_at
```

Зачем:

* понять, что агент реально сделал;
* связать review с конкретным типом изменения;
* видеть паттерны: “на маленькие задачи агент постоянно создаёт много файлов”;
* потом делать правило: “если light_work изменяет >N файлов — спросить подтверждение”.

Минимально можно хранить не отдельной сущностью, а через `/treview reason` и `mcp_call_log`, но это будет хуже.

---

# 4. User manual edits after agent work

Очень ценное сырьё: **что пользователь руками поправил после агента**.

Если агент сделал PR/изменения, а пользователь потом вручную переписал часть кода — это почти идеальный learning signal.

```text
ManualCorrection
- intent_id
- file_path
- before_fragment?
- after_fragment?
- reason?
- created_at
```

Зачем:

* review говорит “не так”, но manual edit показывает “как правильно”;
* это лучший материал для future examples;
* можно извлекать coding preferences без лишних вопросов.

В MVP можно не делать. Но в будущем это сильнее, чем обычный review.

---

# 5. Decision points агента

Не только “что сделал”, но и **какие развилки он выбрал**.

Пример:

```text
AgentDecision
- intent_id
- decision
- alternatives_considered[]
- chosen_alternative
- rationale
- confidence
- created_at
```

Пример содержимого:

```text
decision:
"Создать отдельный сервис для обработки review"

alternatives:
- расширить существующий IntentService
- создать ReviewService
- оставить inline handler

chosen:
"создать ReviewService"

rationale:
"review выглядит отдельной сущностью"

confidence:
0.62
```

Зачем:

* review часто критикует не код, а архитектурный выбор;
* можно учить инструкции на уровне принятия решений;
* полезно для твоей идеи “AI понял масштаб задачи неправильно”.

Но это опасно тащить в MVP: агент начнёт писать много мусора. Лучше включать только для `new_project` или крупных решений.

---

# 6. Uncertainty log

Фиксировать места, где агент **сомневался**, но всё равно пошёл дальше.

```text
UncertaintySignal
- intent_id
- mode
- uncertainty
- assumed_answer
- should_have_asked_question: bool
- created_at
```

Пример:

```text
uncertainty:
"Не ясно, нужно ли создавать отдельный сервис"

assumed_answer:
"Да, создать отдельный сервис"

should_have_asked_question:
true
```

Зачем:

* показывает, где interview должен был задать вопрос;
* помогает улучшать правило “когда спрашивать, а когда действовать”;
* это сырьё для instruction типа: “если изменение влияет на архитектурную границу — спроси”.

---

# 7. Question quality signal

Ты уже сохраняешь `intent_qa`, но не сохраняешь качество вопроса.

Полезно добавить позже:

```text
QuestionFeedback
- intent_qa_id
- quality              // useful | too_detailed | too_broad | unnecessary | missed_question
- note?
```

Зачем:

* понять, какие вопросы раздражают;
* отличать хороший interview от бюрократии;
* улучшать “one-question-at-a-time” поведение.

Пример команды:

```text
/tq плохой вопрос, слишком рано спрашивать про тип данных
```

Но это уже не MVP. Скорее future dogfooding.

---

# 8. Missed-question signal

Очень важный отдельный тип review:

```text
Агент должен был спросить, но не спросил.
```

Это не совсем обычный review. Его стоит явно выделить.

```text
MissedQuestion
- intent_id
- situation
- question_agent_should_have_asked
- consequence
```

Пример:

```text
situation:
"Агент начал реализацию MCP tool"

question_agent_should_have_asked:
"Нужно ли делать tool атомарным через transaction уже сейчас?"

consequence:
"Пришлось переделывать repository layer"
```

Зачем:

* это напрямую улучшает interview;
* это показывает границы самостоятельности агента;
* из этого рождаются лучшие instruction patches.

---

# 9. Scope expansion signal

Для твоего продукта это особенно важно.

Агент часто будет ошибаться так:

```text
маленькая задача → большая архитектура
```

Нужно явно ловить:

```text
ScopeDrift
- intent_id
- expected_scope       // tiny | small | medium | large
- actual_scope         // small | medium | large
- drift_kind           // overengineering | underengineering | wrong_layer | wrong_abstraction
- note
```

Пример:

```text
expected_scope:
small

actual_scope:
large

drift_kind:
overengineering

note:
"Создал отдельный сервис вместо расширения существующего модуля"
```

Это можно сначала хранить внутри `intent_review.reason`, но как отдельная аналитическая категория оно очень ценно.

---

# 10. Repo/context fingerprint

Чтобы понимать, почему агент вел себя так, нужно знать контекст исполнения.

```text
ExecutionContext
- intent_id
- repo_name
- branch
- commit_sha_before
- commit_sha_after?
- working_directory
- detected_stack[]
- created_at
```

Зачем:

* один и тот же Intent в разных репозиториях требует разного поведения;
* instruction может зависеть от стека;
* можно воспроизводить work-сессию;
* удобно для future eval.

Даже если Throne не хранит связь `Intent -> repo`, можно хранить **эпизодический execution context** как лог, не как canonical связь.

---

# 11. Test/verification result

Для coding work это один из самых сильных outcome-сигналов.

```text
VerificationRun
- intent_id
- command
- exit_code
- passed
- summary
- created_at
```

Пример:

```text
command:
bash scripts/quality/verify.sh

passed:
false

summary:
"Architecture test failed: Infrastructure referenced from Domain"
```

Зачем:

* отличать субъективный review от объективного failure;
* понимать, какие изменения ломают quality harness;
* строить instruction: “перед завершением всегда запускать verify”.

Часть этого может быть в agent logs, но лучше сохранить структурированно.

---

# 12. Accepted/rejected instruction proposals

Когда появится `InstructionProposal`, важнейшее сырьё — не только принятые, но и отклонённые предложения.

```text
InstructionProposalDecision
- proposal_id
- decision       // accepted | rejected | edited
- user_reason?
```

Почему rejected тоже важно:

* показывает, какие обобщения были неправильными;
* предотвращает повторное предложение одной и той же ерунды;
* даёт negative examples для будущего learning agent.

---

# 13. “Preference facts”

Это не instruction, а маленькие стабильные предпочтения пользователя.

Пример:

```text
PreferenceFact
- scope              // global | repo | project | mode
- text
- confidence
- source_refs[]
- status             // active | rejected | superseded
```

Примеры:

```text
Для light_work предпочитать минимальные изменения существующих модулей.
```

```text
Не создавать отдельный WorkRun в MVP без явного решения.
```

```text
PRD не должен содержать enum values и API contracts.
```

Это похоже на memory, но лучше не смешивать с Instruction:

* `Instruction` — как агент должен действовать;
* `PreferenceFact` — что пользователь стабильно предпочитает;
* `InstructionProposal` — предложение превратить preference в правило поведения.

---

# Что я бы добавил первым

Не всё сразу. Я бы добавил три вещи.

## 1. `/taccept`

Минимальная команда принятия результата.

```text
/taccept [intentId?] [note?]
```

Сохраняет:

```text
IntentOutcome
- accepted
- note
```

Это закрывает positive learning.

---

## 2. Instruction bundle versions в audit summary

Без новой сущности.

При `get_instruction_bundle(mode)` писать в `mcp_call_log.result_summary`:

```text
mode
instruction_kinds
instruction_versions
```

Это критично для анализа “какие инструкции дали какой результат”.

---

## 3. Structured review classification

Не менять tool, но усилить `reason`.

Например, агент при `/treview` должен заполнять `reason` в формате:

```text
Problem:
...

Likely cause:
...

Future instruction signal:
...

Scope:
intent_only | instruction_candidate | both
```

Это даст почти тот же эффект, что новая модель, но без расширения API.

---

# Приоритетная карта сырья

| Сырьё                               |        Ценность | Сложность | Когда                                  |
| ----------------------------------- | --------------: | --------: | -------------------------------------- |
| `IntentOutcome` / `/taccept`        |   Очень высокая |    Низкая | Сразу после MVP                        |
| Instruction bundle version snapshot |   Очень высокая |    Низкая | Сейчас                                 |
| Structured review reason            |   Очень высокая |    Низкая | Сейчас                                 |
| Verification/test results           |         Высокая |   Средняя | После первых coding runs               |
| Code diff summary                   |         Высокая |   Средняя | После `/twork` dogfooding              |
| Manual corrections                  |   Очень высокая |   Высокая | Позже                                  |
| Missed-question signal              |         Высокая |   Средняя | После interview dogfooding             |
| Scope drift signal                  |         Высокая |   Средняя | После нескольких overengineering cases |
| Agent decision log                  | Средняя/высокая |   Высокая | Только для `tnew`                      |
| Preference facts                    |         Высокая |   Средняя | Перед instruction proposals            |
| Instruction proposal decisions      |   Очень высокая |   Средняя | Вместе с `/tlearn`                     |

---

# Самая компактная next-модель

Я бы расширил не сильно:

```text
intent_outcome
instruction_proposals
```

И чуть улучшил существующее:

```text
intent_review.reason becomes structured
mcp_call_log.result_summary includes instruction versions
```

То есть ближайшая эволюция:

```text
MVP:
intent_qa
intent_review
text_versions
mcp_call_log

MVP+:
intent_outcome
structured review reason
instruction bundle versions in log

Next:
instruction_proposals
proposal decisions
verification summaries
```

Главный недостающий сигнал сейчас: **принятие успешной работы**. Без него система будет учиться только на ошибках.
