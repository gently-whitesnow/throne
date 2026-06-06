# ADR-0028: Quality harness recalibration — limits to where good code lands

## Status

Accepted
Date: 2026-06-05
Related: [ADR-0025](0025-domain-aggregate-style-rich-ddd.md) (amends its budget-override mechanism), [ADR-0023](0023-mcp-tools-snake-case-naming.md)

## Context

Аудит `.quality` harness по месяцу сессий (228 транскриптов) показал, что strict-профиль
`backend-maintainability` фрикционит не диффузно, а на двух конкретных осях, и проект уже
де-факто признал это исключениями:

- **`constructorMaxDependencies: 5`** — самый частый отказ. Уже залатан четырьмя override'ами
  (controllers→15, MCP tools→10, handlers→8, Domain→9999). Лимит, которому нужно столько
  исключений, выставлен ниже зоны приземления оркестрирующего кода. По распределению 477 типов
  P98 сервисных классов ≈ 8 deps; «5» режет нормальные handler'ы/контроллеры.
- **Type-level cyclomatic complexity** — энфорсилась дважды: Roslyn-аналайзером `CA1502(Type) ≤10`
  (через `TreatWarningsAsErrors`) **и** косвенно через budget. Это породило ~43 сессии
  косметических сплитов (`*Factory`/`*Guards`/`*Mutator`/`*Module`), ~11 per-file
  `severity = none` секций и комментарии-«отмазки» по всему дереву. Operator-фидбэк прямо
  назвал это давлением харнеса (мотивация ADR-0025).

Дополнительно:

- Чекер считал параметры primary-конструктора `record`-типов как «зависимости» — DTO с 8–14
  полями ловились метрикой DI-связности, к которой данные отношения не имеют.
- Round-number LOC-лимиты давали boundary-friction: `81 > 80` валил blocking-gate, хотя метр
  одной строки не является проблемой поддерживаемости.

## Decision

1. **Cyclomatic (`CA1502`) и class coupling (`CA1506`) аналайзеры выключены.** Единый
   source-of-truth для этих измерений — `.quality` maintainability budget: per-method cyclomatic
   ≤10 и file fan-out ≤15. Двойной энфорс убран; ~11 per-file dodge-секций и пороги
   `CA1502/CA1506` в `CodeMetricsConfig.txt` удалены. `CA1501` (inheritance depth) и `CA1505`
   (maintainability index) остаются — у них нет budget-эквивалента.

2. **`constructorMaxDependencies` поднят 5 → 8** (P98 сервисных классов). Override'ы controllers→15
   и MCP tools→10 остаются (NSwag-арифметика и tool-fan-out by design); handler-override
   (ctor:8) поглощён базой.

3. **`record`-типы исключены из `constructorMaxDependencies`** на уровне чекера: их primary-ctor
   параметры — поля данных, а не инжектируемые коллабораторы. Метрика остаётся для классов.

4. **`methodLocToleranceFactor: 1.25`** — method-LOC нарушение фиксируется только при overshoot
   за `limit × 1.25`. Гасит boundary-friction round-number лимитов, не открывая дверь крупным
   методам.

5. **`Throne.Domain` выведен из size-бюджета через `exclude`** вместо pathOverride со значениями
   `9999`. Это честнее: budget не делает вид, что проверяет Domain. Rich-DDD инвариант
   (ADR-0025) охраняет уже существующий `DomainEncapsulationRulesTests` (NetArchTest, «no
   `internal set`») — он активен и зелён, а не «вводится отдельным PR». **Amends ADR-0025 §How:**
   механизм Domain-послабления — exclude + NetArchTest, не budget-override `9999`.

## Consequences

- Меньше движущихся частей: один лимит на измерение, без зеркальных аналайзер/budget порогов.
- Лимиты становятся встречаемыми, а не обходимыми — давление на косметические сплиты снято.
- Type-level cyclomatic больше не hard-gate. Это сознательная уступка: именно она плодила
  сателлиты. Реальную сложность ловят per-method CC ≤10 (строже прежнего CA1502 ≤30), typeMaxLoc,
  и code review.
- Гейт остаётся ratchet-blocking на новых нарушениях; baseline пуст и не растёт (изменение —
  только послабление, новых нарушений не вносит).

## Alternatives rejected

- **Оставить «5» как backstop, добавив ещё override'ы** — лечит симптом, не причину; харнес
  продолжает аккретить исключения.
- **Инвертировать ratchet-first (absolute caps → широкий backstop, baseline = рабочая планка)** —
  более глубокая перестройка; вынесена за рамки этого прохода, фиксируется как следующий шаг.
- **Переписать ADR-0025** — ADR — исторический record; механизм правится amend-указателем, а не
  переписыванием.

## Follow-up (out of scope)

- Re-inline косметически расщеплённых сателлитов и удаление stale `// CA1502 …` комментариев в
  `apps/api/src` — отдельный проход (код, не харнес).
- Ratchet-first инверсия size-лимитов.
