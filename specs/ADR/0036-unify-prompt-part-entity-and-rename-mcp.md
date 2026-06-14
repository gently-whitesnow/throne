# ADR-0036: Единая сущность PromptPart (схлопывание Instruction) + сквозное переименование MCP/контрактов

## Status

Accepted
Date: 2026-06-12
Заменяет раннюю модель prompt parts с двумя сущностями (`Instruction` + отдельный `PromptPart`) на одну — см. Context.
Amends [ADR-0014](0014-mcp-initialize-instructions-routing.md) (backing бандла), [ADR-0022](0022-frontier-driven-dream-flow.md) (target патчей), [ADR-0023](0023-mcp-tools-snake-case-naming.md) (одноразовое сквозное переименование контракта), [ADR-0002](0002-domain-model-and-text-versioning.md) (новый owner-kind истории).
Related: [ADR-0025](0025-domain-aggregate-style-rich-ddd.md), [ADR-0030](0030-mcp-surface-policy-cli-first.md), [ADR-0034](0034-dual-execution-contours-hooks-vs-bundles.md).

## Context

Ранняя модель prompt parts сознательно завела **две** сущности: `Instruction` (legacy whitelist `common/interview/work/dream/schema_map` + system, источник правды бандла для standalone-агентов) и отдельный `PromptPart` (optional runtime-части под embedded-терминал). Mandatory-инструкции при этом не дублировались, а **проецировались** в `EffectivePart` из манифеста.

На практике две сущности дают два жизненных цикла, два хранилища (`instructions` collection + манифест YAML vs `prompt_parts`), два патч-контура и два резолвера (`get_instruction_bundle` vs `PromptCompositionResolver`). Dream-патчи таргетят только legacy `InstructionKindNames`. Оператор хочет вести и доулучшать **единый** набор частей в одной модели, где инструкции — это просто mandatory-части, а новые — optional. Это инвертирует прежнюю развилку: не «расширяем whitelist инструкций optional-kind'ами», а «растворяем инструкции в parts».

Имена `*_instruction_*` на MCP/HTTP-поверхности после схлопывания стали бы вечным legacy-долгом. Все потребители ходят через манифест/skills/server-instructions самого Throne (mini-router в `InitializeResult.instructions`, [ADR-0014](0014-mcp-initialize-instructions-routing.md)), контракт под нашим контролем — есть окно атомарно переименовать его без обратных алиасов.

## Decision

### Одна сущность `PromptPart`

`Instruction` как отдельный агрегат, `InstructionKindNames`-whitelist и коллекция `instructions` удаляются. Их роль выполняет `PromptPart`:

- `id`, стабильный `key` (уникален в пределах `scope`), `scope ∈ {system, user}`, `text`, `description`, `current_version`, `created_at`, `updated_at`.
- `mode_roles[] = {mode, role, order}`; `role ∈ {mandatory, default_on, default_off}`; отсутствие записи режима ⇒ часть недоступна в режиме. Whitelist как закрытый список исчезает — его роль выполняет `role=mandatory`.
- `mode` — объединение бандл-режимов (`interview/work/dream/schema_map`) и embedded-режимов (`work/interview/free`). Legacy-инструкции получают `mandatory`-роли в тех режимах, где их `(scope, kind)` встречался в `bundles[].includes` манифеста; `order` — позиция include.

### Append-only история (амендит [ADR-0002](0002-domain-model-and-text-versioning.md))

Унифицированная модель несёт **append-only `text_versions`** (как нынешний `Instruction`), а не только счётчик: части стали патч-таргетами dream и должны иметь историю развития. Вводится `TextVersionOwnerKind.PromptPart`. `PromptPart.ReplaceText` возвращает `TextVersion` (delta), репозиторий пишет её транзакционно; `Create` пишет v1-snapshot.

### Бандл: контент тот же, backing — `prompt_parts` (амендит [ADR-0014](0014-mcp-initialize-instructions-routing.md))

Резолвер бандла читает `prompt_parts`: для режима берёт части с `role=mandatory` в этом режиме, упорядочивает по `order`. Тексты правил байт-в-байт прежние (миграция копирует verbatim), поэтому агент-видимый контент бандла не меняется. Тест-инвариант **«bundle ≡ projection»** (`PromptCompositionResolver` mandatory-проекция ≡ `get_prompt_bundle`) сохраняется и теперь тривиально-истинен: оба резолвера читают один источник.

Манифест [throne-skills.yaml](../manifest/throne-skills.yaml) перестаёт быть runtime-источником текста. Он остаётся:
- **seed-спецификацией** mandatory `system`-частей (`system_instructions` тексты) и композиции (`bundles[].includes` → какие `(scope, key)` mandatory в каком режиме и в каком порядке);
- источником `dream_sources` (без изменений).

Идемпотентный стартовый сервис (`PromptPartSeeder`, паттерн hosted-service из [ADR-0019](0019-intent-events-unified-history.md)) на каждом старте сверяет `system`-части с манифестом (создаёт отсутствующие как v1, при расхождении текста — пишет новую версию) и реконсайлит их `mode_roles`. `user`-части он не трогает.

### Миграция и retire (паттерн [ADR-0022](0022-frontier-driven-dream-flow.md))

Тот же стартовый сервис одноразово переносит `user`-инструкции из коллекции `instructions` в `prompt_parts` (`scope=user`, `key=kind`, текущий текст как **первую** версию — без бэкфила исторических `text_versions`, согласовано), `mode_roles` из includes манифеста. Затем коллекция `instructions` и её индексы, а также `text_versions` с `owner_kind=instruction` — **дропаются при старте**. Перенос идемпотентен (skip, если `prompt_part (scope=user, key)` уже есть). Потеря старой истории инструкций допустима.

### Один patch-агрегат `PromptPartPatch`

Домен `InstructionPatch → PromptPartPatch`. Target патча — `(scope, key)` вместо `target_kind`, но patchable scope закрыт до `scope=user`: `system`-части manifest-managed и на каждом старте реконсайлятся из YAML, поэтому операторские патчи к ним были бы перетёрты seeder'ом. `base_version` = `current_version` целевой user-части, optimistic concurrency на apply (409 needs_rebase). Apply, отсутствующей пока user-части (`base_version=0`), лениво создаёт её c `mode_roles` из манифеста (как миграция). **Apply остаётся операторским** (UI/HTTP `/improvements`); в MCP — только `propose` + чтения, новых write-tool'ов не вводим ([ADR-0030](0030-mcp-surface-policy-cli-first.md)).

### Сквозное переименование (чистый cutover, без алиасов — амендит [ADR-0023](0023-mcp-tools-snake-case-naming.md))

| Было | Стало |
|---|---|
| `get_instruction_bundle` | `get_prompt_bundle` |
| `get_current_instruction` | `get_current_prompt_part` |
| `propose_instruction_patch` | `propose_prompt_part_patch` |
| `list_instruction_patches` | `list_prompt_part_patches` |
| `get_instruction_patch` | `get_prompt_part_patch` |
| параметр `target_kind` | `target_scope` + `target_key` |

Переименование сквозное: домен (`PromptPartPatch`), Application-хендлеры, OpenAPI-модули (`instruction-patches` → `prompt-part-patches`; endpoints `instructions`-модуля `bundles-tree`/`versions`/CRUD сливаются в существующий `prompt-parts`-модуль, отдельный `instructions`-модуль ретайрится), HTTP-роуты, realtime-события `instruction_patch.*` → `prompt_part_patch.*`, манифест/skills/server-instructions (mini-router, dream-бандл), frontend-потребители (codegen + `/improvements` + страница бандлов). Полу-переименованного кода не остаётся: cutover атомарный в одном PR.

[ADR-0023](0023-mcp-tools-snake-case-naming.md) фиксировал имена как стабильный контракт; данный ADR разрешает **одноразовое** сквозное переименование при схлопывании сущности, поскольку контур потребления (mini-router) под контролем Throne и обратная совместимость standalone-агентам не нужна (авто-доставка router'а на каждом handshake).

## Consequences

### Positive

- Один жизненный цикл, одно хранилище (`prompt_parts`), один резолвер, один patch-агрегат. Dream таргетит user-части по `(scope=user, key)`, не только legacy-kind'ы.
- Бандл и embedded-композиция by construction читают один источник — расхождение невозможно.
- Имена поверхности перестают быть legacy-долгом.

### Negative / Risks

- Атомарный cutover большого объёма (домен/app/persistence/MCP/OpenAPI/manifest/skills/frontend/tests) — нет промежуточного собирающегося состояния, PR крупный.
- `system`-части теперь дублируются в Mongo (seed из манифеста) — манифест остаётся авторской поверхностью, стартовый сервис обязан реконсайлить, иначе правка YAML «не доезжает».
- Потеря исторических `text_versions` legacy-инструкций при миграции (осознанно).
- Внешние standalone-агенты со старыми зашитыми именами тулов сломаются до перечитывания router'а — приемлемо для local-first ([ADR-0029](0029-local-first-invariant-and-legacy-auth.md)).

### Out of scope

- Frontend-менеджмент частей сверх переименования потребителей (И4.3).
- LLM-автоотбор частей под задачу; системные optional-части; персист выбора optional между запусками.
- MCP write-tool на apply.
