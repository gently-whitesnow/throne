---
adr: 0011
title: DreamRun aggregate, dream context as token counter, server-managed dream context
status: Accepted
relates_to:
  - ADR-0002
  - ADR-0003
  - ADR-0004
  - ADR-0007
---

# ADR-0011 — DreamRun, dream context as token counter, server-managed dream context

## Status

Accepted.

Расширяет [ADR-0007](0007-vendor-skill-launchers.md) (vendor skill launchers вводят kind `dream`)
и опирается на [ADR-0002](0002-domain-model-and-text-versioning.md) (text-versioning),
[ADR-0003](0003-mcp-text-editing-semantics.md) (text-editing semantics),
[ADR-0008](0008-realtime-contract-first-events.md) (realtime через domain events).

## Context

`/dream` — ручная команда пользователя для self-improvement loop пользовательских инструкций.
Раньше readiness считался как **взвешенный score** по разнородным «evidence»-источникам
(`intent_review`, `intent_qa`, `mcp_call_log.outcome=error`) с severity-весами и порогами
`warming_up → ready → rich`, которые блокировали запуск /dream. Этот дизайн оказался плохим
в трёх местах:

1. **Веса непрозрачны пользователю.** «Score 27 / threshold 40» не отвечает на вопрос
   «что именно туда пойдёт и хватит ли этого». High-severity bug и тысяча тривиальных qa
   в одной метрике мешают калибровке.
2. **MCP-ошибки попадали в обучение /dream.** Они относятся к улучшению самого MCP-инструментария
   и не должны идти в обучение user instructions.
3. **Блокирующий порог конфликтует с моделью «ручная команда».** Если пользователь хочет
   потренироваться на одной единице сигнала — он должен мочь.

Нужны три вещи:

1. **Где живут предложения до их применения?**
   `text_versions` — линейная история **применённых** правок инструкций. Положить туда
   pending dream-proposal до решения пользователя — сломать `current_version` /
   `expected_version`.
2. **Кто решает, что именно идёт в /dream?**
   Сервер должен фиксировать снимок входов в момент создания run, чтобы агент не путался
   в свежих/незавершённых записях и не подбирал контекст «по своему усмотрению».
3. **Как мерить накопленный материал так, чтобы это было одновременно и UI-индикатором, и
   честным отражением «сколько пойдёт в обучение»?**
   Простая метрика — общее число токенов уникального контента, который реально попадёт
   в /dream.

## Decision

Доменная сущность **DreamRun** с embedded **DreamProposal** остаётся. `text_versions`
неизменна: применённый proposal порождает новый TextVersion через
`IInstructionRepository.ReplaceTextAsync` (секция `## Learned rules`). Pending proposal
живёт **только** внутри DreamRun со статусами `pending|applied|skipped`.

### Снимок входа

DreamRun хранит `IntentRefs[]` — список intent'ов, попавших в snapshot, с per-intent
`token_count` (cl100k_base) и `snapshotted_at`. Полей `WindowStart/End` нет: /dream —
ручная команда, и ввод не ограничен временным окном. Поля `EvidenceRefs/EvidenceCounts/
OmittedEvidenceCounts/ReadinessScore` удалены вместе с весовой моделью.

### Что входит в контекст /dream

Сервер берёт **все intents, у которых есть хотя бы одна запись `intent_qa` или
`intent_review`** (минус intents, обработанные closed-processed run-ами, и intents,
зарезервированные открытыми pending run-ами). Никакого временного окна и никаких
`safety_lag`/`max_window_days` — если пользователь дёрнул /dream, он уже завершил
свои процессы и хочет учиться на всём, что накопилось. Для каждого такого intent
в /dream идёт:

- полная история `Intent.text` (`text_versions`, `version` ASC: snapshot/replace/insert);
- финальный `Intent.text` — **дедуплицирован** против последнего version-snapshot/new_text/
  insert_text (если ничего не менялось — не добавляем повторно);
- все `intent_qa` для этого intent (`created_at` ASC);
- все `intent_review` для этого intent (`created_at` ASC).

Внутри агенту материал подаётся двумя «пачками» (формат описан в `tdream` skill playbook):

1. **«улучшение ревью»** — история изменений `Intent.text` + финальный `Intent.text` +
   `intent_qa`;
2. **«улучшение работы»** — финальный `Intent.text` + `intent_review`.

Финальный `Intent.text` фигурирует один раз и используется обеими пачками; токены
считаются по объединению уникального контента.

### Token counter

`Throne.Application.Ports.ITokenizer` — порт; реализация `SharpTokenTokenizer` в
Infrastructure поверх SharpToken (`cl100k_base`, MIT, без native deps), Singleton.
`ContextTokenCounter` (Application) делает per-intent токенизацию: концатенирует
`text_versions ⊕ финальный текст ⊕ qa ⊕ reviews`, токенизирует и суммирует.
Attachments (`IntentAttachmentDocument`) не входят — out of scope (бинарные блобы).

Метрика `available_tokens` — индикатор в UI («накоплено N токенов из M intents»).
Никаких порогов нет — `/dream` запускается всегда, когда пользователь так решит.

### Status

Три состояния:

- `empty` — нет ни одного intent с qa/review;
- `has_content` — есть intents с qa/review, можно запускать /dream;
- `pending_review` — есть открытые DreamRun, надо разобраться сначала с ними.

Все три — informational; ни один не блокирует MCP-вызов `run_dream`. Tool возвращает
`status="not_enough_context"` только в одном крайнем случае — `intent_count==0` —
чтобы агент не создавал бессмысленный пустой run.

### Apply

Apply proposal — единственное место, где DreamRun касается линейной истории инструкций.

1. `BaseInstructionVersion` proposal-а должна совпасть с `current_version` инструкции;
   иначе → `409 dream.proposal.needs_rebase` (без мутации).
2. Сервер берёт текущий `Instruction.Text`, инжектит `final_rule` (или `proposed_rule`)
   в секцию `## Learned rules` (создаёт секцию, если её нет), вызывает
   `IInstructionRepository.ReplaceTextAsync(...)` с `expectedVersion = BaseInstructionVersion`.
3. Помечает proposal `applied`, фиксирует `AppliedInstructionVersion`.
4. Если все proposals run-а имеют `Decision != pending` — auto-close.

### Domain invariants

- `IntentRefs.Count ≥ 1` (DreamRun без intents не существует);
- `TokenCount ≥ 0`;
- `IntentRefs` distinct по `IntentId`;
- `Run.Proposals.Count ≤ 5`;
- severity ↔ intents: `high ≥ 1`, `medium ≥ 2`, `low ≥ 3` distinct intent_refs внутри
  proposal;
- auto-close — только когда `Proposals.Count ≥ 1` и все `Decision != pending`.

### Конфигурация

Секция `Throne:Dream` удалена целиком: все поля (`Weights`, `Thresholds`,
`SafetyLagMinutes`, `MaxWindowDays`) ушли вместе с весовой моделью и временным окном.

### Realtime

`dream.run_created`, `dream.proposal_created`, `dream.proposal_applied`,
`dream.proposal_skipped`, `dream.run_closed` — фанаут через стандартный pipeline ADR-0008.
`dream.fuel_changed` (payload: `{available_tokens, status}`) — debounced best-effort
индикатор; UI всегда может попросить актуальный снимок через
`GET /api/v1/dream-runs/readiness`.

## Альтернативы (отвергнуто)

1. **Pending TextVersion в `text_versions`.** Ломает линейность ADR-0002 и контракт
   `current_version` / `expected_version`.
2. **Агент сам пагинирует raw evidence без Run-а.** Размывает ответственность, нет защиты
   от свежих/незавершённых сессий, нет аудита «что было предложено и что выбрали».
3. **Сохранить severity-веса, но как UI-метрику.** Эмерджентный параметр, который
   пользователь не понимает, не отражает реального объёма контекста и расходится
   с моделью «ручной запуск /dream».
4. **Разрешить агенту самому собирать full intent payloads вместо снимка IntentRefs.**
   Агент будет делать неконтролируемые запросы и подмешивать активные сессии.
5. **Включить `mcp_call_log.error` в /dream через токенайзер.** Эти ошибки относятся
   к улучшению MCP-инструментария, не к user instructions; смешивание заставляло /dream
   предлагать правила для /dream — нерелевантно. Сейчас MCP-ошибки в обучение /dream не идут.

## Consequences

### Positive

- Метрика `available_tokens` прозрачна: пользователь видит, сколько контента уйдёт в /dream.
- `text_versions` остаётся честной линейной историей применённых правок инструкций.
- Сервер владеет «что читать» — агент получает готовый снимок IntentRefs.
- Конфигурации нет: ноль настроечных параметров для /dream.
- `mcp_call_log` отделён от обучения /dream.
- /dream — ручная команда; пользователь сам решает, когда контекст готов, без
  непрозрачных порогов и временных окон.

### Negative / Risks

- Без cap по токенам один /dream может потащить в контекст десятки тысяч токенов.
  В v1 принят как allowed (single-user, малые объёмы); cap добавим, если станет проблемой.
- Свежие qa/review активной рабочей сессии попадут в /dream сразу, без задержки.
  Митигация: пользователь сам выбирает момент запуска /dream. Если окажется неудобным,
  отдельной задачей добавим session-aware фильтр (`session_id` в qa/review writes).
- Race между `apply` и параллельным редактированием инструкции — решается через
  `BaseInstructionVersion` + `409 needs_rebase`.

## Update — Intent 4 (MCP surface)

Server-managed MCP-инструменты для `/tdream` поверх той же модели:

- `mcp__throne__run_dream(policy="auto")` — создаёт pending DreamRun, если есть хоть один
  intent с qa/review активностью. Возвращает дискриминированное поле
  `status`: `created` / `not_enough_context` / `existing_pending`. Идемпотентность
  за 24 часа по последнему pending run-у. Контекст агенту никогда не отдаётся сырьём — только
  `evidence_summary { intent_count, token_count, existing_learned_rules_by_kind }` плюс
  `intent_refs` для повторного использования в `propose_dream_rule`.
- `mcp__throne__propose_dream_rule(run_id, target_kind, proposed_rule, intent_refs[],
  rationale, severity)` — сервер валидирует подмножество `intent_refs` (агент не может
  ссылаться на чужие intents), severity-min (high≥1/medium≥2/low≥3 distinct intents),
  `target_kind ∈ {common, interview, work, fix}`, кэп `MaxProposals = 5`.

`apply_dream_proposal`, `close_dream_run` и `get_dream_readiness` в MCP surface не появляются:
apply — исключительно user-action через HTTP/UI; auto-close сервер выполняет сам;
readiness снапшот живёт только как HTTP-эндпоинт `/dream/readiness` для UI-виджета
fuel-meter — агенту он не нужен, потому что `run_dream` сам считает readiness и принимает
решение запускать ли цикл.

### Update 2026-05-03 — пустой /tdream (no_proposals)

Раннее в этом разделе предполагался MCP-вызов `close_empty_dream_run` для пути «нет
proposals». От этого отказались осознанно: закрытие dream-run должно оставаться
решением человека-оператора, не агента.

- Если агент после `run_dream` не сформулировал ни одного `propose_dream_rule`, он
  сообщает пользователю «ничего не нашёл» и завершает работу. Run остаётся `pending`.
- Накопленное evidence продолжает быть привязано к открытому run и не возвращается в
  общий пул до закрытия.
- Дальнейшее решение принимает оператор через UI: либо явно закрыть пустой run
  (тогда серверный handler `CloseEmptyDreamRunHandler` отрабатывает с
  `release_evidence=true` и evidence снова доступно), либо оставить run открытым,
  накопить ещё qa/review и запустить `/tdream` позже.
- HTTP `POST /api/v1/dream-runs/{runId}/close` (в т.ч. для пустых run) и handler
  `CloseEmptyDreamRunHandler` сохраняются — они теперь обслуживают только UI-кнопку
  «закрыть» и forced-close c proposals. На MCP surface эта операция не отражается.

## Не делаем здесь

- MCP-ошибки (`mcp_call_log.outcome=error`) и их анализ — out of scope этого ADR.
- Token cap / OmittedIntents в DreamRun — без cap для v1.
- Session-aware фильтр через `session_id` в `intent_qa`/`intent_review` — открытая
  тема на будущее, если safety_lag окажется недостаточным.
- UI `/dream` (Intent 5) и `/tdream` playbook (Intent 6).
- Cross-process realtime fanout (см. ADR-0009 — открыт).
