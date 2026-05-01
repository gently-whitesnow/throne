# ADR Registry

## Foundational (affect all code)
<!-- Cross-cutting decisions: architecture style, storage, deployment model -->

- [ADR-0001](0001-foundation-clean-architecture-monorepo.md) — Foundation: Clean Architecture в монорепо `apps/api` + (будущий) `apps/web`. .NET 10, MongoDB, official MCP SDK, CPM, xUnit + Testcontainers.
- [ADR-0002](0002-domain-model-and-text-versioning.md) — Domain model `Intent`/`Instruction`: компактный canonical-документ (`text` + `current_version` + `tags`), единая коллекция `text_versions` в delta-формате (v1 snapshot, v2+ replace/insert), отдельные коллекции `intent_qa` / `intent_review` (training-only, агенту невидимые), optimistic concurrency через `expected_version` + typed `ApiException`.
- [ADR-0003](0003-mcp-text-editing-semantics.md) — MCP text-editing semantics: 9 tools для MVP (4 read + 5 write Intent). Отдельный `add_intent_qa` (decoupled от edit), actionable error codes (`match_not_found` / `match_ambiguous` / `version_conflict` / `line_out_of_range`), серверный `get_instruction_bundle(mode)` как единственный путь работы агента с инструкциями. Инструкции редактируются пользователем напрямую (mongosh / будущий HTTP), агентского write-surface для них нет. Запрет `full_replace` / `replace_by_line_range` / `list_*` / `include_text?`.
- [ADR-0004](0004-mcp-call-audit-log.md) — MCP call audit log: append-only коллекция `mcp_call_log` (tool_name, arguments, intent_id, session_id, outcome, error_code, duration), middleware на границе `Throne.Api`, best-effort запись через порт `IMcpCallLogSink`. Гарантия покрытия by construction (единая registration-helper + architecture test + startup fail-fast + параметризованный smoke-тест). База для dogfooding-телеметрии и будущего обучения системы.

## Module-scoped (affect one bounded context)
<!-- Decisions scoped to specific modules -->
