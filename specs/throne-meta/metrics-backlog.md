# Throne metrics backlog

Live-документ. Новые метрики добавляются через обычный Intent с действием «Update metrics-backlog.md: ...».

Status: `proposed` | `partially` | `shipped`.

Источник `collect-raw §N` ссылается на разделы `thoughts/collect_raw_sources.md` (см. историю git, commit `71aa3d7`).

## Кандидаты

| # | Metric | What it captures | Insight it gives | Status | Source |
|---|---|---|---|---|---|
| 1 | `IntentOutcome` | Финальный исход Intent: `accepted` / `rejected` / `partially_accepted` / `abandoned` + опциональная заметка пользователя. | Отличает «принято» от «пользователь просто ушёл», даёт positive examples и базу для eval dataset. | proposed | collect-raw §1 |
| 2 | `InstructionBundleUse` | Snapshot use инструкций (`mode`, `kind`, `instruction_id`, `version`) на момент interview/work. | Связывает поведение агента с конкретной версией инструкций, позволяет сравнивать `light_work vN` vs `vN+1`. | proposed | collect-raw §2 |
| 3 | `WorkDeltaSummary` | Компактный след результата work: `changed_files`, `added_files`, `deleted_files`, `commands_run`, `git_diff_stat`, `test_results`. | Связывает review с типом изменения, ловит паттерны вроде «light_work плодит файлы». | proposed | collect-raw §3 |
| 4 | `ManualCorrection` | Ручные правки пользователя поверх агентского результата: `file_path`, `before/after_fragment`, `reason`. | Лучший learning signal «как правильно», основа для извлечения coding preferences. | proposed | collect-raw §4 |
| 5 | `AgentDecision` | Развилки агента: `decision`, `alternatives_considered`, `chosen_alternative`, `rationale`, `confidence`. | Учит правила на уровне архитектурных решений, ловит «AI неверно понял масштаб». | proposed | collect-raw §5 |
| 6 | `UncertaintySignal` | Места, где агент сомневался, но пошёл дальше: `uncertainty`, `assumed_answer`, `should_have_asked_question`. | Показывает где interview должен был задать вопрос; сырьё для правил «когда спрашивать». | proposed | collect-raw §6 |
| 7 | `QuestionFeedback` | Качество вопроса interview: `useful` / `too_detailed` / `too_broad` / `unnecessary` / `missed_question`. | Отделяет полезный interview от бюрократии, улучшает one-question-at-a-time. | proposed | collect-raw §7 |
| 8 | `MissedQuestion` | Ситуация, где агент должен был спросить, но не спросил: `situation`, `question_agent_should_have_asked`, `consequence`. | Прямое улучшение interview, граница самостоятельности агента, источник лучших instruction patches. | proposed | collect-raw §8 |
| 9 | `ScopeDrift` | Расхождение ожидаемого и фактического масштаба: `expected_scope`, `actual_scope`, `drift_kind` (`overengineering` / `underengineering` / `wrong_layer` / `wrong_abstraction`). | Аналитическая категория «маленькая задача → большая архитектура», основной таргет для twork-инструкций. | proposed | collect-raw §9 |
| 10 | `ExecutionContext` | Контекст исполнения: `repo_name`, `branch`, `commit_sha_before/after`, `working_directory`, `detected_stack`. | Один Intent в разных репо требует разного поведения; основа для воспроизводимости и future eval. | proposed | collect-raw §10 |
| 11 | `VerificationRun` | Запуски проверок: `command`, `exit_code`, `passed`, `summary`. | Объективный outcome-сигнал поверх субъективного review; ловит ломку quality harness. | proposed | collect-raw §11 |
| 12 | `InstructionProposalDecision` | Решение по InstructionProposal: `accepted` / `rejected` / `edited` + `user_reason`. | Rejected тоже сырьё: блокирует повторные плохие предложения, даёт negative examples. | proposed | collect-raw §12 |
| 13 | `PreferenceFact` | Стабильные предпочтения пользователя: `scope` (`global` / `repo` / `project` / `mode`), `text`, `confidence`, `source_refs`, `status`. | Промежуточный слой между memory и Instruction; кандидат на превращение в правило. | proposed | collect-raw §13 |

## Already shipped via Intent 2/4

| Metric | What it captures | Status | Source |
|---|---|---|---|
| `ReadinessScore` (weighted dream fuel) | Взвешенная готовность к Dream-проходу по типам сырья. | shipped | Intent 2 (ADR-0011) |
| `DreamRun.EvidenceCounts` | Разбивка собранных evidence по типам внутри одного DreamRun. | shipped | Intent 2 |
| `DreamRun.OmittedEvidenceCounts` | Причины отбрасывания evidence: `too_recent` / `budget_exceeded` / `low_priority`. | shipped | Intent 2 |
| `DreamProposal.skipped_reason` | Почему конкретный DreamProposal не был выдвинут. | shipped | Intent 4 |
| Session-aware `safety_lag` exclusion | Исключение сырья из активной сессии при сборе fuel. | shipped | Intent 2 |
