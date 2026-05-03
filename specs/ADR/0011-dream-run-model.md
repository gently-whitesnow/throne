---
adr: 0011
title: DreamRun aggregate, weighted readiness, server-managed dream context
status: Accepted
relates_to:
  - ADR-0002
  - ADR-0003
  - ADR-0004
  - ADR-0007
---

# ADR-0011 — DreamRun, weighted readiness, server-managed dream context

## Status

Accepted.

Расширяет [ADR-0007](0007-vendor-skill-launchers.md) (vendor skill launchers вводят kind `dream`)
и опирается на [ADR-0002](0002-domain-model-and-text-versioning.md) (text-versioning),
[ADR-0003](0003-mcp-text-editing-semantics.md) (text-editing semantics),
[ADR-0004](0004-mcp-call-audit-log.md) (mcp_call_log как источник evidence),
[ADR-0008](0008-realtime-contract-first-events.md) (realtime через domain events).

## Context

`/tdream` (см. ADR-0007 §6) собирает накопившийся feedback (`intent_review`, `intent_qa`, ошибки
из `mcp_call_log`, будущие `verification_run` / `manual_correction` / `intent_outcome`) и
предлагает точечные правки в `instructions` под управлением пользователя. Чтобы это работало
коректно, нужно ответить на три вопроса:

1. **Где живут предложения до их применения?**
   `text_versions` — линейная история **применённых** правок инструкций (snapshot v1, затем
   replace/insert v2+). Если положить туда «висящий» дream-proposal до решения пользователя,
   `current_version` начнёт обозначать совсем другое («предложено», а не «применено»),
   `expected_version` теряет смысл, а bundle resolver рискует выдать агенту неутверждённое
   правило. Альтернативу с pending TextVersion отвергаем — она ломает контракт ADR-0002.
2. **Кто решает, какое evidence пора варить в правило?**
   Вариант «агент сам ходит и читает сырые reviews/qa/mcp_log» переносит на агента
   ответственность за объём, свежесть и приоритет. Это даёт нестабильное поведение и риск
   подхватить ещё-не-завершённую активную сессию (ту самую, в которой `/tdream` запущен).
   Альтернатива «ручной фильтр в каждом MCP-инструменте» расползается по местам. Нужна одна
   серверная стадия, фиксирующая снимок входов на момент создания run.
3. **Как мерить, что проблем «накопилось достаточно»?**
   Простая «количеством записей за N дней» метрика не различает high-severity баг и тысячу
   тривиальных qa. Нужна весовая модель, которую можно тюнить без выпуска кода.

## Decision

Введена доменная сущность **DreamRun** с embedded **DreamProposal**. `text_versions`
остаётся неизменной: применённый proposal порождает обычный новый TextVersion через
существующий `IInstructionRepository.ReplaceTextAsync`, как любое user-driven правило.
Pending proposal **никогда не превращается в TextVersion**; он живёт исключительно внутри
DreamRun со статусами `pending|applied|skipped`.

Серверный «бак топлива» (readiness) пересчитывается по конфигурируемым весам и порогам.
Расчёт читает evidence из источников (intent_review, intent_qa, mcp_call_log с outcome=error;
далее — verification_run, manual_correction, accepted_outcome; см. «Не делаем здесь»),
ограничивается **safe time window** (`now − safety_lag` сверху, последний закрытый
DreamRun или `now − 90d` снизу) и **session-aware фильтром**: всё evidence сессии,
которая писала в `mcp_call_log` за последние `safety_lag` минут, исключается из расчёта,
даже если запись старше окна. Это снимает ровно тот сценарий, когда `/tdream`
запущен в активной рабочей сессии и пытается «учиться» на собственных свежих ошибках.

DreamRun, созданный MCP-инструментом `run_dream` (Intent 4), фиксирует:

- `WindowStart` / `WindowEnd` — окно, по которому собрано evidence;
- `EvidenceRefs[]` — конкретные `(Kind, Id)` записей **сырья** (не intent_id);
- `EvidenceCounts` / `OmittedEvidenceCounts` — breakdown для UI и debug;
- `ReadinessScore` — взвешенный балл на момент создания.

Когда DreamRun закрывается (auto после решения по всем proposals или manual через
`POST /api/v1/dream-runs/{runId}/close`), `EvidenceRefs` становятся «processed»: повторный
расчёт readiness их игнорирует. Manual close пустого run по умолчанию **не** помечает
evidence processed — пользователь хочет «попробовать ещё раз». Явный `release_evidence: true`
в close-теле перезаписывает default.

Apply proposal — единственное место, где DreamRun касается линейной истории инструкций.
Шаги (`POST /proposals/{id}/apply`):

1. `BaseInstructionVersion` proposal-а должна совпасть с `current_version` инструкции;
   иначе → `409 dream.proposal.needs_rebase` (без мутации).
2. Сервер берёт текущий `Instruction.Text`, инжектит `final_rule` (или `proposed_rule`)
   в секцию `## Learned rules` (создаёт секцию, если её нет), вызывает
   `IInstructionRepository.ReplaceTextAsync(...)` с `expectedVersion = BaseInstructionVersion`.
3. Помечает proposal `applied`, фиксирует `AppliedInstructionVersion`.
4. Если все proposals run-а имеют `Decision != pending` — auto-close.

Domain invariants (закрепляются доменными тестами):

- severity ↔ evidence: `high` ≥ 1 ref, `medium` ≥ 2, `low` ≥ 3 (security/safety/data-loss
  helper-ы режутся как high даже при 1).
- `Run.Proposals.Count ≤ 5` (защита от спама).
- Auto-close — только когда `Proposals.Count ≥ 1` и все `Decision != pending`.

### Веса readiness (config `Throne:Dream:Weights`, начальные значения)

| Evidence kind                      | Вес  |
|------------------------------------|------|
| `intent_review` (severity ≠ high)  | +5   |
| `intent_review` severity=high      | +10  |
| `verification_failure`             | +5   |
| `manual_correction`                | +8   |
| `mcp_call.outcome=error`           | +2   |
| `intent_qa`                        | +1   |
| `accepted_outcome`                 | +3   |
| `skipped_proposal_with_reason`     | +4   |

### Пороги (`Throne:Dream:Thresholds`)

- `< 10` → `warming_up`;
- `10..40` → `ready`;
- `≥ 40` → `rich`;
- любое high-severity ref сразу даёт `ready` независимо от score.

### Realtime

`dream.run_created`, `dream.proposal_created`, `dream.proposal_applied`,
`dream.proposal_skipped`, `dream.run_closed` — фанаут через стандартный pipeline ADR-0008
(domain events на outcome → `RealtimeDomainEventHandler`). `dream.fuel_changed` —
debounced best-effort: эмитим только на «значимых» evidence-write
(add_intent_review, add_intent_qa, dream.* mutations); `mcp_call_log.outcome=error`
realtime НЕ триггерит (слишком шумно). UI всегда может попросить актуальный снимок через
`GET /api/v1/dream-runs/readiness`.

## Альтернативы (отвергнуто)

1. **Pending TextVersion в `text_versions`.** Ломает линейность ADR-0002 и контракт
   `current_version` / `expected_version`.
2. **Агент сам пагинирует raw evidence без Run-я.** Размывает ответственность, нет защиты
   от свежих/незавершённых сессий, нет аудита «что было предложено и что выбрали».
3. **Простой счётчик evidence без весов.** Не различает high-severity bug и тривиальный qa.
   Тюнить порог под реальные нагрузки придётся пересборкой.
4. **Хранение DreamRun в отдельной коллекции на каждый kind инструкции.** Дробит запросы
   readiness и теряет «единый бак топлива» процесса. Embedded proposals в одном run-е
   проще читать и нагляднее в UI.

## Consequences

### Positive

- `text_versions` остаётся честной линейной историей применённых правок инструкций.
- Server владеет «сколько/что/когда читать» — агент просит готовый bounded
  `DreamContextPack` (Intent 4 ставит этот endpoint поверх ADR-0011).
- Веса и пороги меняются конфигом, без релиза.
- Session-aware фильтр устраняет класс ошибок «учусь на собственной свежей сессии».
- UI получает консистентный fuel meter и список pending proposals по одному
  endpoint-у `readiness`, без склейки на клиенте.

### Negative / Risks

- Появляется новая агрегатная коллекция `dream_runs` и сопутствующие индексы.
- Веса/пороги — emergent параметр; неудачные значения дадут «warming_up навсегда» или
  слишком агрессивные предложения. Митигировано: значения в config + фиксация снапшота
  весов в run-е (`ReadinessScore`).
- Race между `apply` и параллельным редактированием инструкции пользователем — решается
  через `BaseInstructionVersion` + `409 needs_rebase`. Пользователь должен повторно открыть
  proposal после рефреша.

## Не делаем здесь

- MCP-инструменты `run_dream` / `propose_instruction_change` (Intent 4).
- Источники evidence `verification_run`, `manual_correction`, `intent_outcome` —
  заведём, когда появятся соответствующие сущности; сегодня readiness считает только
  `intent_review`, `intent_qa`, `mcp_call_log.outcome=error`, `skipped_proposal_with_reason`.
- UI `/dream` (Intent 5) и `/tdream` playbook (Intent 6).
- Cross-process realtime fanout (см. ADR-0009 — открыт).
