# ADR-0036: Единая сущность PromptPart (схлопывание Instruction) + сквозное переименование MCP/контрактов

## Status

Accepted
Date: 2026-06-12
Заменяет раннюю модель prompt parts с двумя сущностями (`Instruction` + отдельный `PromptPart`) на одну — см. Context.
Amends [ADR-0014](0014-mcp-initialize-instructions-routing.md) (backing бандла), [ADR-0022](0022-frontier-driven-dream-flow.md) (target патчей), [ADR-0023](0023-mcp-tools-snake-case-naming.md) (одноразовое сквозное переименование контракта), [ADR-0002](0002-domain-model-and-text-versioning.md) (новый owner-kind истории).
Related: [ADR-0025](0025-domain-aggregate-style-rich-ddd.md), [ADR-0030](0030-mcp-surface-policy-cli-first.md), [ADR-0034](0034-dual-execution-contours-hooks-vs-bundles.md).
**Amended 2026-06-19** — bundle как доставка плейбука выпилен, см. ниже.
**Amended 2026-06-21** — system-части больше не материализуются в Mongo: read-path берёт их напрямую из манифеста через `IPromptPartRepository`, `PromptPartSeeder` удалён.

**Update 2026-06-21:** MCP-переименование и tool-поверхность из этого ADR retired by [ADR-0043](0043-static-operational-skills-and-mcp-removal.md) — MCP-сервер и `apps/api/src/Throne.Api/Mcp` удалены, операции (`propose_prompt_part_patch`, `set_intent_status`/`create_intent`, apply) теперь статические repo-скилы + `skills/<id>/bin/throne-*`. Схлопывание сущности `PromptPart` и `prompt_parts` как единственное хранилище остаются в силе.

## Amendment (2026-06-19): bundle removed, standalone = knowledge base

Схлопывание сущностей и переименование (ниже) остаются в силе — `PromptPart` единственная модель; `user` runtime-части хранятся в `prompt_parts`, `system`-части синтезируются из манифеста. Что изменилось:

- MCP-тул `get_prompt_bundle` (строка в таблице переименования) **удалён целиком** вместе с `bundles-tree` HTTP-эндпоинтом, `PromptBundleResolver`/`PromptBundleRenderer`, UI визуализацией бандлов и тест-инвариантом «bundle ≡ projection» (§ «Бандл: контент тот же»). Standalone-агент больше не получает плейбук по MCP — Throne для него база знаний интентов (read/write `Intent.text` + `set_intent_status`/`create_intent` по явной просьбе; см. [ADR-0034](0034-dual-execution-contours-hooks-vs-bundles.md)).
- `bundles[].includes` в манифесте **сохранены** и продолжают питать `PromptPartManifestRoles` (вывод mandatory `mode_roles`) и `PromptCompositionResolver`. Композиция читается напрямую резолвером, без промежуточного bundle-резолвера.
- Где ниже сказано «бандл читает `prompt_parts`» / «`get_instruction_bundle → get_prompt_bundle`» — читать как историю до 2026-06-19. Embedded-композиция (`PromptCompositionResolver`) и dream-патчи не затронуты.

## Context

Ранняя модель prompt parts сознательно завела **две** сущности: `Instruction` (legacy whitelist `common/interview/work/dream` + system, источник правды бандла для standalone-агентов) и отдельный `PromptPart` (optional runtime-части под embedded-терминал). Mandatory-инструкции при этом не дублировались, а **проецировались** в `EffectivePart` из манифеста.

На практике две сущности дают два жизненных цикла, два хранилища (`instructions` collection + манифест YAML vs `prompt_parts`), два патч-контура и два резолвера (`get_instruction_bundle` vs `PromptCompositionResolver`). Dream-патчи таргетят только legacy `InstructionKindNames`. Оператор хочет вести и доулучшать **единый** набор частей в одной модели, где инструкции — это просто mandatory-части, а новые — optional. Это инвертирует прежнюю развилку: не «расширяем whitelist инструкций optional-kind'ами», а «растворяем инструкции в parts».

Имена `*_instruction_*` на MCP/HTTP-поверхности после схлопывания стали бы вечным legacy-долгом. Все потребители ходят через манифест/skills/server-instructions самого Throne (mini-router в `InitializeResult.instructions`, [ADR-0014](0014-mcp-initialize-instructions-routing.md)), контракт под нашим контролем — есть окно атомарно переименовать его без обратных алиасов.

## Decision

### Одна сущность `PromptPart`

`Instruction` как отдельный агрегат, `InstructionKindNames`-whitelist и коллекция `instructions` удаляются. Их роль выполняет `PromptPart`:

- `id`, стабильный `key` (уникален в пределах `scope`), `scope ∈ {system, user}`, `text`, `description`, `current_version`, `created_at`, `updated_at`.
- `mode_roles[] = {mode, role, order}`; `role ∈ {mandatory, default_on, default_off}`; отсутствие записи режима ⇒ часть недоступна в режиме. Whitelist как закрытый список исчезает — его роль выполняет `role=mandatory`.
- `mode` — объединение бандл-режимов (`interview/work/dream`) и embedded-режимов (`work/interview/free`). Legacy-инструкции получают `mandatory`-роли в тех режимах, где их `(scope, kind)` встречался в `bundles[].includes` манифеста; `order` — позиция include.

### Append-only история (амендит [ADR-0002](0002-domain-model-and-text-versioning.md))

Унифицированная модель несёт **append-only `text_versions`** (как нынешний `Instruction`), а не только счётчик: части стали патч-таргетами dream и должны иметь историю развития. Вводится `TextVersionOwnerKind.PromptPart`. `PromptPart.ReplaceText` возвращает `TextVersion` (delta), репозиторий пишет её транзакционно; `Create` пишет v1-snapshot.

### Бандл: контент тот же, backing — `IPromptPartRepository` (амендит [ADR-0014](0014-mcp-initialize-instructions-routing.md))

Резолвер композиции читает через `IPromptPartRepository`: для режима берёт mandatory-части в порядке `bundles[].includes`, где `system` приходит из манифеста, а `user` — из `prompt_parts`. Тексты правил байт-в-байт прежние, поэтому агент-видимый контент композиции не меняется.

Манифест [throne-skills.yaml](../manifest/throne-skills.yaml) остаётся runtime-источником mandatory `system`-частей (`system_instructions` тексты) и композиции (`bundles[].includes` → какие `(scope, key)` mandatory в каком режиме и в каком порядке). Он также остаётся:
- источником `dream_sources` (без изменений).

`system`-части читаются из манифеста через manifest-backed реализацию `IPromptPartRepository`: list/get для scope=`system` синтезируют read-only `PromptPart` с детерминированным id `system:{kind}` и ролями из `PromptPartManifestRoles`. Mongo-документы scope=`system` не читаются и не реконсайлятся. `user`-части создаются явно через UI или patch-apply flow и живут в Mongo как versioned/editable runtime-данные.

### Миграция и retire (паттерн [ADR-0022](0022-frontier-driven-dream-flow.md))

Legacy-миграция `instructions` → `prompt_parts` была частью исходного cutover ADR-0036: `user`-инструкции переносились как `scope=user`, `key=kind`, текущий текст как **первая** версия — без бэкфила исторических `text_versions`, согласовано. Текущего startup-сервиса для prompt parts больше нет; `system`-документы в Mongo не мигрируются и не чистятся.

### Один patch-агрегат `PromptPartPatch`

Домен `InstructionPatch → PromptPartPatch`. Target патча — `(scope, key)` вместо `target_kind`, но patchable scope закрыт до `scope=user`: `system`-части manifest-managed и меняются только PR-ом к манифесту. `base_version` = `current_version` целевой user-части, optimistic concurrency на apply (409 needs_rebase). Apply, отсутствующей пока user-части (`base_version=0`), лениво создаёт её c `mode_roles` из манифеста (как миграция). **Apply остаётся операторским** (UI/HTTP `/improvements`); в MCP — только `propose` + чтения, новых write-tool'ов не вводим ([ADR-0030](0030-mcp-surface-policy-cli-first.md)).

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

- Один patch-агрегат и один repository-интерфейс для композиции. Dream таргетит user-части по `(scope=user, key)`, не только legacy-kind'ы.
- System-композиция by construction читает манифест как единственный источник текста — дрейф Mongo-копии невозможен.
- Имена поверхности перестают быть legacy-долгом.

### Negative / Risks

- Атомарный cutover большого объёма (домен/app/persistence/MCP/OpenAPI/manifest/skills/frontend/tests) — нет промежуточного собирающегося состояния, PR крупный.
- Потеря исторических `text_versions` legacy-инструкций при миграции (осознанно).
- Внешние standalone-агенты со старыми зашитыми именами тулов сломаются до перечитывания router'а — приемлемо для local-first ([ADR-0029](0029-local-first-invariant-and-legacy-auth.md)).

### Out of scope

- Frontend-менеджмент частей сверх переименования потребителей (И4.3).
- LLM-автоотбор частей под задачу; системные optional-части; персист выбора optional между запусками.
- MCP write-tool на apply.
