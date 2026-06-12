# ADR-0035: Provider-neutral модель prompt parts и embedded-композиция

## Status

Accepted
Date: 2026-06-12
Related: [ADR-0034](0034-dual-execution-contours-hooks-vs-bundles.md), [ADR-0014](0014-mcp-initialize-instructions-routing.md), [ADR-0025](0025-domain-aggregate-style-rich-ddd.md), [ADR-0006](0006-openapi-contract-first-codegen.md), [ADR-0030](0030-mcp-surface-policy-cli-first.md)

## Context

Встроенный терминал ([ADR-0034](0034-dual-execution-contours-hooks-vs-bundles.md)) собирает стартовый контекст из управляемого набора «частей промпта» под выбранный режим и показывает его оператору в пред-запусковой модалке. Сегодняшний резолвер бандлов оперирует фиксированным набором `{scope, kind}` includes из манифеста (`common/interview/work/dream/schema_map`). Этот набор — закрытый whitelist пользовательских инструкций и target для dream-патчей ([ADR-0014](0014-mcp-initialize-instructions-routing.md), [ADR-0022](0022-frontier-driven-dream-flow.md)).

Опциональные runtime-части (`architecture`, `committing`, `postgres`, `mongo`, …) имеют другой жизненный цикл: их авторит оператор, у них роль зависит от режима (включена всегда / предвыбрана / доступна), и они не должны размывать whitelist инструкций. Расширять `InstructionKindNames` произвольными optional kind — значит смешать две сущности с разным контрактом.

## Decision

Вводим **отдельную provider-neutral модель `PromptPart`** рядом с `Instruction`, а не расширяем kind-whitelist. Существующий MCP-контур `get_instruction_bundle` **не трогаем** — он остаётся source of truth для standalone-агентов ([ADR-0034](0034-dual-execution-contours-hooks-vs-bundles.md)); embedded-композиция строится отдельным резолвером, который проецирует те же mandatory-инструкции.

### Агрегат `PromptPart` (rich-DDD, [ADR-0025](0025-domain-aggregate-style-rich-ddd.md))

- `id`, стабильный `key` (уникален в пределах scope), `scope: system|user`, `text`, `description`, `current_version`, `created_at`, `updated_at`.
- `mode_roles[]` — `{mode, role, order}`. `role ∈ {mandatory, default_on, default_off}`; отсутствие записи режима = часть недоступна в режиме.
- `editable`/`present` для UI вычисляются на проекции (см. ниже), а не хранятся на агрегате.
- Версионируем только счётчиком `current_version` (без append-only истории `text_versions`): части короткие и эфемерные по влиянию, полноценный delta-журнал [ADR-0002](0002-domain-model-and-text-versioning.md) избыточен.

`PromptPart` — это **user optional parts**: runtime-данные в Mongo (коллекция `prompt_parts`), авторит оператор. Системных optional parts пока не вводим (scope зарезервирован).

### Mandatory-части — проекция, не дубль

Сегодняшние `system_instructions` (манифест) и user-инструкции `common/interview/work/dream` остаются единственным хранилищем своих текстов. В embedded-композиции они **проецируются** в `EffectivePart` с `role=mandatory` (порядок — из includes манифеста). Так MCP-бандл и embedded-композиция читают один и тот же текст: расхождения быть не может by construction, а не по соглашению. Тест `Mandatory projection ≡ MCP bundle` фиксирует инвариант.

### Резолвер композиции

`PromptCompositionResolver(mode, selected_part_ids?, intent_text)` →
`{ parts[] (с role/selected/text/editable/present), system_prompt, user_prompt }`:

- **Модусы**: `work` / `interview` проецируют mandatory из соответствующего бандла манифеста; `free` mandatory не имеет (всё курирует оператор). Это embedded-модусы ([ADR-0026](0026-embedded-terminal-capabilities-and-run-preflight.md)), отдельные от MCP-модусов `dream`/`schema_map`.
- **Выбор**: mandatory всегда включены; optional — пересечение с `selected_part_ids`, если задан, иначе все `default_on`.
- **Порядок**: mandatory (порядок манифеста), затем optional (по `order`).
- `system_prompt` — собранный блок правил (mandatory + выбранные optional) → уходит в `--append-system-prompt`. `user_prompt` — тело интента (draft зоны задачи). Деление зон и его обоснование (адхеренс + prompt caching) — в [ADR-0034](0034-dual-execution-contours-hooks-vs-bundles.md).

### Контракты ([ADR-0006](0006-openapi-contract-first-codegen.md))

Новый модуль `prompt-parts` (своя сборка `Throne.PromptParts.Contracts`): `GET/POST /api/v1/prompt-parts`, `GET /api/v1/prompt-parts/{id}`, `POST /api/v1/prompt-parts/{id}/replace-text`, `PUT /api/v1/prompt-parts/{id}/roles`. Превью композиции — в модуле `terminal`: `POST /api/v1/intents/{intent_id}/terminal/preview`. Frontend получает собранный `system_prompt`/`user_prompt` от backend и сам runtime-промпт не склеивает ([ADR-0030](0030-mcp-surface-policy-cli-first.md): MCP-surface для parts не вводим — это UI/embedded-контур).

## Consequences

- Whitelist инструкций остаётся закрытым; optional-части эволюционируют независимо.
- `get_instruction_bundle` байт-в-байт совместим: его код не менялся, mandatory-тексты те же.
- Появляется новая Mongo-коллекция `prompt_parts` (уникальный индекс `(scope, key)`).
- Пред-запусковая модалка и менеджмент частей строятся поверх этих контрактов отдельными слайсами; preflight-spawn с прокидыванием `system_prompt`/`user_prompt` — за рамками этого решения.
- Системные optional parts, релевантностный отбор и персист выбора между запусками — out of scope.
