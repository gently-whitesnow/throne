# ADR-0007: Vendor skill launchers вместо MCP prompts как UX-вход в Throne

## Status

Accepted

## Context

UX-входом в Throne workflow были MCP prompts (`tnew, twork, tinterview, treview`), зарегистрированные в `Throne.Api.Mcp.Prompts.IntentPrompts`. Это давало slash-команды поверх MCP, но получило две проблемы:

1. **Vendor parity.** Cursor и Codex ненадёжно показывают MCP prompts как user-facing команды; пользователь не может вызвать workflow одинаково из всех клиентов.
2. **Coupling серверной логики к релизу `Throne.Api`.** Текст playbook'а лежал в C# атрибутах и `IntentPrompts.*Playbook`; обновление playbook требовало нового релиза сервера, а не правки данных в `instructions`.

Альтернативы:

1. **Оставить MCP prompts.** Не решает vendor parity и продолжает связывать playbook-текст с релизом сервера.
2. **Vendor-локальные «толстые» команды (полный playbook в `.claude/commands/*.md` и т.п.).** Решает parity, но ломает [ADR-0003](0003-mcp-text-editing-semantics.md): playbook агента снова живёт вне Throne, и обновления требуют PR в каждый клиентский каталог.
3. **Тонкие vendor launcher-файлы, делегирующие в `get_instruction_bundle`.** Vendor-файлы знают только: «возьми bundle для mode=X, следуй ему». Live playbook остаётся серверным (`EnsureSeedInstructionsHandler` + `instructions` collection), обновляется правкой данных, агент через MCP получает актуальный текст на каждый вызов.

Выбран вариант 3.

## Decision

1. **MCP prompts как UX-канал убраны.** `IntentPrompts.cs` и его регистрация удалены. Инфраструктура `AddThronePrompt<T>()` / `AuditingMcpServerPrompt` оставлена для будущих use-case'ов, startup-ассерт «должен быть хотя бы один prompt» снят. MCP **tools** не меняются.
2. **Launcher = thin pointer.** Каждый launcher — один markdown ≤120 строк (целевой потолок ~60), который содержит только:
   1. Resolve intent (или явное «no intent context» для `tdream`).
   2. `mcp__throne__get_instruction_bundle(mode=<X>, intent_id?=...)`.
   3. Follow returned bundle. **Server bundle overrides anything written in the launcher.**
   4. Persist outcome через MCP по директивам bundle.
   5. Surface `missing_kinds`, не импровизировать.
   6. Return: intent id, что изменилось, next step.
3. **Vendor layout.**

   | Vendor | Каталог |
   |---|---|
   | Codex, Cursor (shared) | `.agents/skills/<name>/SKILL.md` |
   | Claude Code | `.claude/skills/<name>/SKILL.md` |

   Body launcher'ов идентичен между вендорами; различия допустимы только во frontmatter, если конкретный клиент того требует.
4. **Launcher-имена и режимы.** Имена короткие, без префиксов:

   | Launcher | Bundle mode | Назначение |
   |---|---|---|
   | `tnew` | `new_project` | Создать или продолжить intent для нового проекта |
   | `twork` | `work` | Точечная работа по текущему intent в репо |
   | `tinterview` | `interview` | Уточнить постановку, по одному вопросу за шаг |
   | `tfix` | `fix` | Зафиксировать review через `add_intent_review` и продолжить работу. Заменяет `treview`. |
   | `tdream` | `dream` | Свести накопленный фидбэк, оформить proposals; никакой автоактивации |

5. **Backend для `tdream`.** Введён instruction kind `dream`, bundle mode `dream → [common, dream]`. Системный текст хранится в `SystemInstructionCatalog` (см. update 2026-05-02 ниже). Без новых tools: dream-агент пишет proposals как `add_intent_review` на соответствующих Instruction Intent'ах с `reason="instruction_patch_proposal"`. Прямого write-surface для Instruction-документов у агента не появляется (см. [ADR-0003](0003-mcp-text-editing-semantics.md)).
6. **Traceability InstructionBundleUse.** Отдельная сущность не вводится. Каждый вызов `get_instruction_bundle` уже логируется через [ADR-0004](0004-mcp-call-audit-log.md) (`mcp_call_log`: `tool_name + arguments(mode, intent_id) + session_id + outcome + duration + server_version`). Если этого окажется мало для аналитики (нужны конкретные `current_version` снапшоты используемых instructions), вводится отдельный tool `record_instruction_bundle_use(...)` — но не сейчас.
7. **Контракт «launcher тонкий».** Файл launcher >120 строк рассматривается как смелл: значит серверная логика снова потекла локально. Лечится переносом в соответствующий instruction kind на сервере, не правкой launcher'а.
8. **Будущий installer.** Установщик Throne в чужой проект генерирует `.agents/skills/` и `.claude/skills/` из этих эталонных файлов. Launcher'ы практически не меняются — только при vendor format breaks или добавлении/удалении launcher-имени.

## Consequences

- Vendor parity: одна и та же команда `tnew/twork/tinterview/tfix/tdream` доступна во всех целевых клиентах через их штатный skill/rule механизм.
- Playbook эволюционирует данными в `instructions`, а не релизами `Throne.Api`. Это согласуется с принципом «Throne строится для других проектов; сам Throne — на manual SDD».
- Старая команда `treview` исчезает как видимое имя (заменена на `tfix`); инфраструктурные tool description'ы обновлены. Backward-compat для имени `treview` не вводим осознанно — UX был сломан и для старого имени.
- Появляется два каталога вендорных файлов в репо (`.agents/skills/`, `.claude/skills/`). Цена принимается: они тонкие, инсталлер в будущем сгенерирует их в чужие проекты по тем же шаблонам.
- Если объём `tdream` вырастет, добавляются точечные MCP tools — namely `list_unprocessed_instruction_feedback`, `propose_instruction_patch`, `mark_instruction_feedback_processed`, опционально `record_instruction_bundle_use`. До тех пор launcher остаётся тонким.

## Update 2026-05-02 — system/user split + work/fix kinds

- Сидинг через `EnsureSeedInstructionsHandler` упразднён. System-инструкции живут в коде как `SystemInstructionCatalog` ([apps/api/src/Throne.Application/Instructions/SystemInstructionCatalog.cs](../../apps/api/src/Throne.Application/Instructions/SystemInstructionCatalog.cs)) и версионируются вместе с релизом `Throne.Api`. Mongo collection `instructions` теперь хранит только `scope=user` записи.
- Документ `instructions` обогащён полями `scope` и `user_id`. MVP-пользователь — `mvp-user`. User-инструкции бутстрапятся отдельным mongosh-скриптом [scripts/seed/seed-mvp-user-instructions.js](../../scripts/seed/seed-mvp-user-instructions.js) (идемпотентный); скрипт же переименовывает legacy `kind=light_work → work` и удаляет legacy system-документы.
- Kinds: `common | interview | work | new_project | dream | fix`. Kind `light_work` переименован в `work` (launcher `twork`). Введён kind `fix` для launcher `tfix` — отдельный режим продолжения работы после review (раньше делил bundle с `light_work`).
- Bundle resolver (`GetInstructionBundleHandler`) собирает `[system:common, system:<mode>]` из catalog и `user:*` инструкции `mvp-user` для тех же kinds. Антагонист в user создаётся под каждый system kind: для `common`, `work`, `new_project` — с реальным текстом, для `interview`, `dream`, `fix` — пустые редактируемые записи.
- `Instruction.Validate` ослаблен: пустой `Text` для user-инструкций легален, чтобы пустые антагонисты были корректным состоянием.
