# ADR-0009 — Cross-process realtime fanout: все MCP-write через Throne.Api

## Status
Accepted.

Update 2026-06-13: механизм доставки (отдельный stdio→HTTP proxy) superseded by
[ADR-0037](0037-direct-http-mcp-for-standalone-agents.md). Standalone-клиенты
теперь подключаются к `Throne.Api /mcp` напрямую; proxy-проект и его publish-workflow
удалены. **Сам инвариант ниже остаётся в силе**: все MCP-write входят в процесс
`Throne.Api`, поэтому мутация, domain events и SSE-фанаут живут в одном процессе.

Update 2026-06-21: MCP transport itself is retired by
[ADR-0043](0043-static-operational-skills-and-mcp-removal.md). The surviving
invariant is process-local writes: operational CLI calls enter `Throne.Api` over
HTTP, and domain events still fan out from that same process.

Тесно связан с [ADR-0008](0008-realtime-contract-first-events.md) — снимает отложенное там ограничение «broker in-memory» в той части, что касается раздельных MCP- и Web-процессов в single-operator local-first деплое ([ADR-0029](0029-local-first-invariant-and-legacy-auth.md)). Cross-instance fanout остаётся открытым ADR поверх seam'а `IRealtimeEventBroker`.

## Context

Воспроизводимый баг: когда агент через MCP меняет данные (`create_intent`, `replace_intent_text`, `set_intent_tags`, ...), UI на `apps/web` не обновляется. Ручной refresh показывает уже изменённые данные — то есть запись прошла, но SSE-кадра не было.

Root cause — мутация шла в одном OS-процессе, а SSE-подписчики висели на брокере другого:

- Ранний stdio MCP-сервер — отдельный процесс, который Claude Code запускал как STDIO MCP server. Он напрямую подключался к Mongo через `AddThroneMcpCore` → `AddThroneInfrastructure`.
- `Throne.Api` — отдельный процесс (порт 5008). Здесь живёт `RealtimeController` (`GET /api/v1/realtime/stream`) и SSE-подписчики из браузера.
- Оба процесса регистрируют один DI-граф: `MongoUnitOfWork` обёрнут `DomainEventDispatchingUnitOfWork`, который фанаутит domain events в `RealtimeDomainEventHandler` → `InMemoryRealtimeBroker`.
- `InMemoryRealtimeBroker` — `ConcurrentDictionary` в памяти процесса, cross-process транспорта нет (так задумано в ADR-0008).

Когда stdio-процесс писал в Mongo, его UoW дёргал локальный broker — там ноль подписчиков, потому что SSE-клиенты висели на брокере процесса `Throne.Api`. С точки зрения UI — тишина.

## Decision

**Все MCP-write обязаны входить в процесс `Throne.Api`**, чтобы мутация, domain events и SSE-фанаут жили в одном процессе и UI обновлялся by construction.

Изначально это обеспечивал тонкий stdio→HTTP MCP-прокси (pass-through на `<base>/mcp`, без своего Mongo-клиента / broker / Application-поверхности). После [ADR-0037](0037-direct-http-mcp-for-standalone-agents.md) standalone-клиенты ходят в `Throne.Api /mcp` напрямую, и proxy удалён — инвариант сохраняется тем же способом: единая точка входа MCP-write.

Следствия топологии:
- Domain events публикуются единственному инстансу `InMemoryRealtimeBroker`, к которому подключены SSE-клиенты браузера.
- `mcp_call_log` пишется ровно один раз (на стороне Api `AuditingMcpServerTool`), а не дважды. Audit-инвариант ([ADR-0004](0004-mcp-call-audit-log.md)) сохранён.
- `IRealtimeEventBroker` остаётся seam'ом для будущей cross-instance имплементации (Redis/NATS) — отдельным ADR, без изменений domain pipeline или фронтового контракта.

## Consequences

Положительные:
- баг «UI не обновляется после MCP-write» уходит: запись и фанаут в одном процессе.
- невозможно «по ошибке» добавить write-tool в обход Api, который пропустит SSE, — нет второго процесса с write-поверхностью.
- audit живёт в одном месте, без двойных записей `mcp_call_log`.

Компромиссы:
- `Throne.Api` должен быть запущен и доступен до старта агента.
- HTTP-роунд-трип на каждый MCP-вызов вместо in-process диспатча — единицы миллисекунд, на восприятии ноль.

## Alternatives considered

- **Mongo outbox + change stream tail.** Domain event дополнительно пишется в `realtime_outbox` тем же UoW; `Throne.Api` держит change stream и фанаутит SSE. Решает проблему даже при реально multi-process write-сценариях. Отклонено: для single-operator local-first избыточно, добавляет постоянный write-overhead и дублирует знание об events. Вернёмся, если появятся несколько write-процессов.
- **Redis/NATS pub-sub.** Чисто, но требует обязательной инфраструктуры и решает проблему cross-instance, которой в local-first нет.

## References

- [ADR-0008](0008-realtime-contract-first-events.md) — realtime pipeline и seam `IRealtimeEventBroker`.
- [ADR-0037](0037-direct-http-mcp-for-standalone-agents.md) — прямой HTTP MCP, удаление proxy.
- [Throne.Api/Realtime/InMemoryRealtimeBroker.cs](../../apps/api/src/Throne.Api/Realtime/InMemoryRealtimeBroker.cs)
- [Throne.Application/Events/DomainEventDispatchingUnitOfWork.cs](../../apps/api/src/Throne.Application/Events/DomainEventDispatchingUnitOfWork.cs)
