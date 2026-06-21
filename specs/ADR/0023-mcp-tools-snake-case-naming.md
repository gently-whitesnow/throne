# ADR-0023 — MCP tool parameter snake_case is legal at the protocol boundary

## Status

Accepted
Date: 2026-05-16
Related: [ADR-0014](0014-mcp-initialize-instructions-routing.md), [ADR-0022](0022-frontier-driven-dream-flow.md)

**Update 2026-06-21:** retired for active code by [ADR-0043](0043-static-operational-skills-and-mcp-removal.md). The `apps/api/src/Throne.Api/Mcp/Tools` boundary and its `.editorconfig` exception were removed.

## Контекст

`apps/api/src/Throne.Api/Mcp/Tools/**/*.cs` — это [MCP] tool surface: имена параметров уходят в JSON-схему тула и становятся wire-форматом, который видят сторонние агенты (Claude Code, Claude Desktop, Codex CLI и т.п.). MCP spec и весь существующий tool-набор throne'а используют `snake_case` (`intent_id`, `from_id`, `target_kind`, …). Менять имена на C#-нативный `camelCase` нельзя — это публичный контракт.

Analyzer CA1707 («Identifiers should not contain underscores») при `TreatWarningsAsErrors=true` блокирует билд каждый раз, когда параметр C#-метода называется по-MCP-шному. До этого ADR симптом затыкался `#pragma warning disable CA1707` в шапке каждого Mcp/Tools/*.cs (7 файлов), а каждая pragma учитывалась в `.quality/suppress-baseline.json` как technical debt.

Это not technical debt: имена — это контракт, который мы не имеем права менять. Pragma-уровень suppression создаёт неверный сигнал «у нас 7 supressions, которые надо разгрести», и одновременно требует ручной баланс при добавлении/удалении тула.

## Решение

1. Перевести правило с pragma-уровня на editorconfig-уровень. В `apps/api/.editorconfig` добавлена секция:

   ```ini
   # ADR-0023: MCP wire-format requires snake_case parameter names; this directory
   # is an API boundary, not normal C# code. CA1707 is structurally illegal here.
   [src/Throne.Api/Mcp/Tools/**.cs]
   dotnet_diagnostic.CA1707.severity = none
   ```

2. Удалить `#pragma warning disable CA1707` из всех 7 файлов в `apps/api/src/Throne.Api/Mcp/Tools/`:
   - `DreamTools.cs`, `InstructionPatchTools.cs`, `IntentAttachmentTools.cs`, `IntentLinkTools.cs`, `IntentStatusTools.cs`, `IntentTextTools.cs`, `IntentTools.cs`.

3. Удалить соответствующие 7 записей `kind=pragma, rule=CA1707` из `.quality/suppress-baseline.json`. Ratchet `suppression_audit.py` это разрешает: общее число записей уменьшается с 81 до 74.

4. `suppression_audit.py` уже игнорирует glob-секции (`if "*" in current_section: continue`) при сборе списка editorconfig-suppressions, поэтому новая `[src/Throne.Api/Mcp/Tools/**.cs]` не попадает в baseline и не оседает в долге.

## Последствия

- Будущее добавление нового MCP-тула с `snake_case` параметрами не требует никаких суппрессий и не дёргает quality gate.
- CA1707 продолжает действовать на весь остальной `Throne.Api` — это per-directory исключение, а не глобальное.
- Editorconfig glob `[src/Throne.Api/Mcp/Tools/**.cs]` совпадает с фактическим расположением MCP-тулов; если структура каталога изменится, секцию переписываем синхронно (рефакторинг таких dir-меток ловит обычный code review).
- Внутри Mcp/Tools/*.cs **разрешено** именовать только параметры MCP-тулов snake_case. Прочий C#-код (private методы, поля, helper-классы) обязан соблюдать обычные CA*-правила; CA1707 на этих именах не сработает, но code review за этим следит.

[MCP]: https://modelcontextprotocol.io/
