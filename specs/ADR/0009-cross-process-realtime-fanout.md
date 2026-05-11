# ADR-0009 — Cross-process realtime fanout: STDIO как тонкий прокси к Throne.Api

## Status
Accepted.

Тесно связан с [ADR-0008](0008-realtime-contract-first-events.md) — снимает явно отложенное там ограничение "broker in-memory; multi-instance потребует внешнего pub/sub — отдельное ADR" в той части, что касается раздельных STDIO- и Web-процессов в self-hosted dev/single-instance деплое. Multi-instance fanout остаётся открытым ADR.

## Context

Воспроизводимый баг: когда агент через MCP меняет данные (`create_intent`, `replace_intent_text`, `set_intent_tags`, ...), UI на `apps/web` не обновляется. Ручной refresh показывает уже изменённые данные — то есть запись прошла, но SSE-кадра не было.

Root cause:

- `Throne.Mcp.Stdio` — отдельный OS-процесс, который Claude Code запускает как STDIO MCP server. До этого ADR он напрямую подключался к Mongo через `AddThroneMcpCore` → `AddThroneInfrastructure`.
- `Throne.Api` — отдельный процесс (docker-compose `apps/api`, порт 5008). Здесь живёт `RealtimeController` (`GET /api/v1/realtime/stream`) и SSE-подписчики из браузера.
- Оба процесса регистрируют один и тот же DI-граф: `MongoUnitOfWork` обёрнут декоратором `DomainEventDispatchingUnitOfWork`, который фанаутит domain events в `IDomainEventHandler`. Единственный handler — `RealtimeDomainEventHandler` пишет в `InMemoryRealtimeBroker`.
- `InMemoryRealtimeBroker` — `ConcurrentDictionary<Guid, Subscription>` в памяти процесса. Cross-process транспорта нет (так и задумано в ADR-0008).

Когда STDIO-процесс пишет в Mongo, его декоратор UoW исправно дёргает локальный `RealtimeDomainEventHandler`, который пишет в локальный `InMemoryRealtimeBroker` — там ноль подписчиков, потому что SSE-клиенты висят на брокере другого процесса. С точки зрения UI — тишина.

Контекст принятия решения:
- Throne сейчас self-hosted-first; SaaS-режим — следующая итерация. Горизонтальный масштаб `Throne.Api` в обозримом будущем не нужен.
- Claude Code на дату ADR не позволяет указать MCP-сервер по plain-HTTP localhost (только STDIO или HTTPS). Поэтому STDIO-процесс остаётся обязательной точкой входа в self-hosted деплое.
- ADR-0008 уже зафиксировал `IRealtimeEventBroker` как seam: любая будущая cross-instance имплементация (Redis/NATS) подключается без изменений domain pipeline или фронтового контракта.

## Decision

`Throne.Mcp.Stdio` превращён в **тонкий STDIO→HTTP MCP прокси**:

- При старте процесс читает `Throne:ApiBaseUrl` (env / config; default `http://localhost:5008`) и поднимает `IMcpClient` через `SseClientTransport` к `<base>/mcp` (auto-detect Streamable HTTP / SSE — оба поддерживаются `app.MapMcp("/mcp")` на Throne.Api).
- `client.ListToolsAsync()` возвращает `IList<McpClientTool>`; для каждого `McpClientTool` (это `Microsoft.Extensions.AI.AIFunction`) регистрируется `McpServerTool.Create(tool)` — pass-through, который инвокирует upstream-инструмент со всеми аргументами «как есть» и возвращает upstream-`CallToolResult` без модификаций.
- Никаких `AddThroneMcpCore` / `AddThroneApplication` / `AddThroneInfrastructure` / `AddThroneRealtime` / `AuditingMcpServerTool` на стороне STDIO. Нет Mongo-клиента, нет realtime-broker, нет SkillManifestProvider.
- Если upstream недоступен на старте — процесс логирует понятную ошибку на stderr и завершается с кодом 1; Claude Code показывает пользователю падение MCP-сервера.

Следствия топологии:
- Все мутации идут через `Throne.Api`. Domain events публикуются единственному инстансу `InMemoryRealtimeBroker`, к которому подключены SSE-клиенты браузера. UI обновляется by construction.
- `mcp_call_log` пишется ровно один раз (на стороне Api `AuditingMcpServerTool`), а не дважды. Audit-инвариант (ADR-0004) сохранён.
- ADR-0008 не меняется. `IRealtimeEventBroker` остаётся seam'ом для будущей Redis/NATS-имплементации; multi-instance `Throne.Api` отделяется отдельным ADR.

## Cloud trajectory

- **Self-hosted single-instance (now)**: одна сборка docker-compose `--profile full`, `Throne.Api` на 5008, веб на 8080, `Throne.Mcp.Stdio` запускается Claude Code и проксирует на 5008. Баг снят без новых зависимостей.
- **SaaS single-instance (next)**: тот же `Throne.Api` под HTTPS, добавляются auth + `user_id` на Intent/Tag/QA/Review (отдельные intents). Брокер остаётся in-memory, потому что инстанс один.
- **SaaS multi-instance (потом)**: новая реализация `IRealtimeEventBroker` поверх Redis Streams или NATS — по seam'у, описанному в ADR-0008. Domain events / outcomes / handlers / фронтовый `useRealtimeEvent` без изменений. Это уже отдельное ADR.

## Consequences

Положительные:
- баг "UI не обновляется после MCP-write" уходит для всех актуальных deploy-сценариев (dev, self-hosted prod, SaaS single-instance).
- `Throne.Mcp.Stdio` теряет всю Application/Infrastructure-поверхность — он больше физически не может выполнять прямые мутации в обход Api. Это закрывает целый класс будущих регрессий: невозможно «по ошибке» добавить new write-tool на Stdio-стороне, который пропустит SSE.
- Audit live в одном месте — никаких двойных записей `mcp_call_log` от двух процессов.
- Stdio-процесс становится простым (~70 строк) и переиспользуемым: тот же бинарник работает и для self-hosted, и для будущего SaaS, и для CI-проверок.

Компромиссы:
- Stdio теперь требует, чтобы `Throne.Api` был запущен и доступен по `Throne:ApiBaseUrl`. Self-hosted-инструкция должна явно говорить "сначала docker compose up, потом запускай Claude Code".
- Tools перечисляются один раз на старте Stdio. Если Throne.Api добавит новый tool на лету, придётся перезапустить Stdio. На практике — некритично: tool surface меняется релизами Api.
- HTTP-роунд-трип на каждый MCP-вызов вместо локального in-process диспатча. Latency растёт на единицы миллисекунд в локальной сети; на пользовательском восприятии ноль.

## Alternatives considered

- **Mongo outbox + change stream tail.** Domain event дополнительно пишется в коллекцию `realtime_outbox` тем же UoW; `Throne.Api` держит change stream и фанаутит SSE. Решает проблему даже если когда-то появятся реально multi-process write-сценарии. Отклонено: для self-hosted single-instance избыточно, добавляет постоянный write-overhead, дублирует знание об events между outcomes и change-stream-маппингом, и не нужно прямо сейчас. Если завтра появятся реально несколько write-процессов (например, отдельный sync-агент), вернёмся к этому варианту или к Redis.
- **Redis/NATS pub-sub для всех deploy-сценариев.** Чисто, but требует обязательной инфраструктуры (Redis в docker-compose, в проде, в CI) и решает проблему, которой пока нет (multi-instance API). Отложено до момента, когда `Throne.Api` пойдёт в multiple instances.
- **Бандл-CLI, который локально поднимает API + UI + STDIO в одном процессе.** Один из исходных вариантов (Option 1 в обсуждении). Решает только dev-кейс, не помогает SaaS multi-instance, добавляет packaging-сложность. Отклонено как упаковочная оптимизация, не архитектурное решение.

## Out of scope

- Auth / `user_id` на Intent/Tag/QA/Review — необходимо перед публичным SaaS, делается отдельным intent'ом.
- Multi-instance `Throne.Api` и cross-instance fanout — будущее ADR поверх seam'а из ADR-0008.
- Удаление `Throne.Mcp.Stdio` целиком — возможно, когда Claude Code/Codex/Cursor разрешат указывать non-HTTPS HTTP MCP-сервер локально. Тогда launcher'ы будут указывать на `http://localhost:5008/mcp` напрямую, а Stdio станет ненужным.

## References

- [ADR-0008](0008-realtime-contract-first-events.md) — текущий realtime pipeline и seam `IRealtimeEventBroker`.
- [Throne.Mcp.Stdio/Program.cs](../../apps/api/src/Throne.Mcp.Stdio/Program.cs)
- [Throne.Api/Program.cs](../../apps/api/src/Throne.Api/Program.cs)
- [Throne.Api/Realtime/InMemoryRealtimeBroker.cs](../../apps/api/src/Throne.Api/Realtime/InMemoryRealtimeBroker.cs)
- [Throne.Api/Realtime/RealtimeDomainEventHandler.cs](../../apps/api/src/Throne.Api/Realtime/RealtimeDomainEventHandler.cs)
- [Throne.Application/Events/DomainEventDispatchingUnitOfWork.cs](../../apps/api/src/Throne.Application/Events/DomainEventDispatchingUnitOfWork.cs)
- [docker-compose.yml](../../docker-compose.yml)
