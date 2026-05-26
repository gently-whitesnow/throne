# ADR-0025: Domain aggregate style — rich-DDD over satellite mutators

## Status

Accepted
Date: 2026-05-26
Related: [ADR-0001](0001-foundation-clean-architecture-monorepo.md), [ADR-0002](0002-domain-model-and-text-versioning.md), [ADR-0023](0023-mcp-tools-snake-case-naming.md)

## Context

В `Throne.Domain` сложился гибридный паттерн «анемичный агрегат + сателлиты». Каждый агрегат (`Intent`, `Instruction`, `InstructionPatch`, `Tag`, `DreamSession`, `IntentRepositoryBinding`) — мешок данных с `internal set` на свойствах состояния; инварианты и переходы вынесены в соседние static-классы с суффиксами `*Factory` / `*Guards` / `*Mutator` / `*Operation` / `*RestoreValidator`.

Иллюстрации стиля:

- `Intent` ([Intent.cs](../../apps/api/src/Throne.Domain/Intents/Intent.cs)) — `public IntentState State { get; internal set; }`; конструктор `internal`; создание через `IntentFactory.Create/Restore` ([IntentFactory.cs](../../apps/api/src/Throne.Domain/Intents/IntentFactory.cs)); инварианты в `IntentGuards` ([IntentGuards.cs](../../apps/api/src/Throne.Domain/Intents/IntentGuards.cs)); смена статуса — `IntentStatusOperation.SetStatus(intent, …)` ([IntentStatusOperation.cs](../../apps/api/src/Throne.Domain/Intents/IntentStatusOperation.cs)), которое снаружи мутирует `intent.State`. Аналогично — `IntentInsertTextOperation` / `IntentReplaceTextOperation` / `IntentMoveOperation` / `IntentTagOperation`.
- `IntentRepositoryBinding` ([IntentRepositoryBinding.cs](../../apps/api/src/Throne.Domain/Repositories/IntentRepositoryBinding.cs)) — `State { get; internal set; }`; переходы как extension-методы `binding.MarkCloning(...)` / `MarkReady(...)` / `MarkFailed(...)` / `MarkBroken(...)` в [IntentRepositoryBindingMutator.cs](../../apps/api/src/Throne.Domain/Repositories/IntentRepositoryBindingMutator.cs); PR-операции вынесены ещё в один файл `IntentRepositoryBindingPullRequestMutator`; восстановление — через [IntentRepositoryBindingRestoreValidator.cs](../../apps/api/src/Throne.Domain/Repositories/IntentRepositoryBindingRestoreValidator.cs); входные DTO для фабрики — `IntentRepositoryBindingInputs`.
- `Tag` ([Tag.cs](../../apps/api/src/Throne.Domain/Tags/Tag.cs)) — единственный instance-метод `Rename` делегирует в static-класс `TagRenameOperation`; `Name` / `CurrentVersion` / `UpdatedAt` помечены `internal set`.
- `DreamSession` ([DreamSession.cs](../../apps/api/src/Throne.Domain/Dreams/DreamSession.cs)) — самый «осознанный» вариант той же модели: `Create`/`Restore` объявлены на агрегате, но делегируют в `DreamSessionFactory` с комментарием «чтобы остаться внутри per-type CA1502 budget».

Ревью PR #5 (slice 1, уже в master) задал вопрос «принят DDD — почему фабрика?», и анализ показал, что это не локальная просадка одного агрегата, а стиль всего Domain.

## Motivation

Гипотеза причины — не сознательное архитектурное решение, а реакция на давление харнеса. Текущий strict-профиль `backend-maintainability` ([../../.quality/maintainability-budget.json](../../.quality/maintainability-budget.json)) ставит на Domain те же лимиты, что на REST-controllers и Application-handlers:

```
fileMaxLoc:400  typeMaxLoc:200  methodMaxLoc:50  typeMaxPublicMembers:12
methodMaxCyclomaticComplexity:10  fileMaxFanOut:15
```

Плюс существующий path-override `constructorMaxDependencies:10` на `**/Throne.Domain/**.cs` — для primary-ctors с большой identity+state.

Rich-агрегат с инлайн-инвариантами, 5–7 transitions и identity+state-properties в эти границы не помещается. Итерация за итерацией Create уезжал в `*Factory`, инварианты — в `*Guards`, mutating-переходы — в extension-методы и `*Operation`. Локально каждое расщепление выглядело уместным; в сумме — анемичная модель с дырой в инкапсуляции (`State { get; internal set; }`).

Это та же ложная нота, что в [ADR-0023](0023-mcp-tools-snake-case-naming.md): когда харнес настойчиво подталкивает прочь от целевого стиля, починка — в харнесе, а не в коде.

## Decision

### 1. Нормативный target style для агрегатов `Throne.Domain`

Rich-DDD: агрегат сам несёт фабрику, инварианты и переходы. Конкретно:

- Конструктор агрегата — `private`. `internal` допустим только для test-fixture или persistence-маппера, и только если нет альтернативы.
- Точки создания — `public static T Create(...)` и `public static T Restore(...)` на самом агрегате. Инварианты выполняются внутри Create / Restore, а не делегируются в отдельный `*Guards`.
- State-переходы — инстансными методами агрегата (`intent.SetStatus(...)`, `binding.MarkCloning(...)`, `tag.Rename(...)`), без extension-методов и без отдельных `*Operation` классов.
- Свойства состояния — `{ get; private set; }` или иммутабельный record-state с приватной заменой через `private` метод. Никакого `internal set` на свойствах агрегата.
- Value-objects (`IntentId`, `BindingId`, `RepoCoordinate`, `FractionalIndex`, …), enum-name классы (`IntentStatusNames`, `CloneStatusNames`, …) и доменные события — стиль не пересматриваем, остаются как есть.

#### Разрешено отдельным файлом

- Domain services поверх нескольких агрегатов или поверх внешнего домен-порта: `IntentTextSearch` (читает несколько `Intent`-ов).
- Чисто-вычислительные хелперы без state и без привязки к одному агрегату:
  - [TextEditMatcher.cs](../../apps/api/src/Throne.Domain/TextEditMatcher.cs)
  - [TextEditLineCount.cs](../../apps/api/src/Throne.Domain/TextEditLineCount.cs)
  - [TextEditLineLookup.cs](../../apps/api/src/Throne.Domain/TextEditLineLookup.cs)
  - `FractionalIndex` и спутники (`FractionalIndexAppend`, `FractionalIndexMidpoint`, `FractionalIndexPrepend`, `FractionalIndexValidator`, `FractionalIndexAlphabet`).

#### Запрещено

Запреты нормативные — контроль на code review (auto-гейт ограничен единственным маркером, см. ниже):

- Отдельные файлы `*Factory` / `*Guards` / `*Mutator` / `*Operation` / `*RestoreValidator`, относящиеся к одному агрегату.
- Mutating extension-методы для агрегата.
- `internal set` на свойствах состояния агрегата.

### 2. Harness adjustments

Лимиты на стиль Domain живут в двух местах, оба нужно лифтить:

- **`.quality/maintainability-budget.json`** — единый pathOverride на `**/Throne.Domain/**.cs` с открытыми значениями `fileMaxLoc` / `typeMaxLoc` / `methodMaxLoc` / `typeMaxPublicMembers` / `methodMaxCyclomaticComplexity` / `fileMaxFanOut` / `constructorMaxDependencies`. Rationale в `_comment` ссылается на этот ADR.
- **`apps/api/.editorconfig`** — секция `[**/Throne.Domain/**.cs]` отключает type-level Roslyn-аналайзеры `CA1501` / `CA1502` / `CA1505` / `CA1506` (Inheritance / CyclomaticComplexity / Maintainability / ClassCoupling). Под `TreatWarningsAsErrors=true` они блокируют `dotnet build` независимо от budget-гейта. Per-method `CA1502 ≤30` остаётся включённым и ловит реальную сложность.

Два существующих Domain-override'а в budget JSON снимаются — поглощаются общим:

- `constructorMaxDependencies:10` на `**/Throne.Domain/**.cs` (был обоснован primary-ctors с identity+state).
- `typeMaxPublicMembers:20` на `**/Throne.Domain/Dreams/DreamSession.cs` (был обоснован полным state-surface'ом aggregate'а).

Глобальный duplicates-гейт (`crossFileOnly`) остаётся как есть — он ловит дублирование между файлами и Domain не требует исключения.

### 3. Regression gate planned

Единственный авто-надзор за стилем — NetArchTest «no `internal set` на свойствах типов из сборки `Throne.Domain`» — вводится отдельным PR после миграции всех 6 агрегатов, чтобы тест встал в зелёное состояние без baseline-исключений. На момент принятия этого ADR гейт ещё не существует.

Кастомные Roslyn analyzer-проекты (`Throne.Domain.Analyzers` и т.п.) не плодим — весь архитектурный надзор остаётся в NetArchTest.

## Alternatives

### A. Сохранить сателлиты, ослабить инкапсуляцию явно (`public set` на State)

Зафиксировали бы статус-кво, но потеряли бы изначальный мотив DDD — агрегат как граница транзакции инвариантов. Анемичность стала бы декларативно принятой, что усложнит будущие проверки «эта операция допустима в текущем состоянии».

### B. Перейти на rich-DDD, не трогая харнес

Каждый rich-агрегат был бы вечной добавкой в `suppress-baseline.json` или в `pathOverrides` per file. Это создаёт ложный сигнал «у нас technical debt» там, где фактически — целевой стиль. Аналогичный случай уже разобран в [ADR-0023](0023-mcp-tools-snake-case-naming.md).

### C. Жёсткие auto-гейты на запрет `*Factory` / `*Guards` через NetArchTest или кастомный Roslyn analyzer

Кодифицировало бы запреты, но рискует ложными срабатываниями на легитимных domain services (`IntentTextSearch`) и на computational helpers. На текущем масштабе (6 агрегатов) ADR + ревью — достаточный страж; auto-гейт ограничиваем единственным маркером инкапсуляции (`internal set`). Если гипотеза «достаточно ADR» опровергнется на следующих итерациях — заведём отдельный интент под расширенный auto-гейт.

## Consequences

- 6 агрегатов (`Tag`, `DreamSession`, `Instruction`, `InstructionPatch`, `IntentRepositoryBinding`, `Intent`) мигрируются в rich-стиль отдельной серией PR — по одному агрегату на PR.
- В Domain исчезают файлы `*Factory.cs` / `*Guards.cs` / `*Mutator.cs` / `*Operation.cs` / `*RestoreValidator.cs`, относящиеся к конкретному агрегату. Допустимые исключения (domain services, computational helpers) перечислены выше.
- `verify.sh` после каждого PR серии остаётся зелёным; baseline в `suppress-baseline.json` по Domain-файлам не пополняется.
- NetArchTest «no `internal set` в Throne.Domain» (Part 3 интента) закрывает регресс по основному маркеру стиля; остальные запреты охраняются code review.

## Out of scope

- Application / Infrastructure: static-сателлиты там остаются уместными — этот ADR не пересматривает их стиль.
- Value-objects, enum-name классы и доменные события: стиль не пересматриваем.
- Кастомные Roslyn analyzer-проекты под Domain: не плодим.
- Auto-гейт на запрет `*Factory` / `*Guards` через NetArchTest: см. Alternatives C.
- 300-строчное правило «file-size lint»: оно не закреплено ни в `CLAUDE.md`, ни в харнесе; вместо него остаётся `fileMaxLoc` strict-профиля с ratchet, и для Domain мы его как раз ослабляем.
