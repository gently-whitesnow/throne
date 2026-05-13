# Throne.Mcp.Stdio

Тонкий STDIO→HTTP MCP-прокси для [Throne](https://github.com/gently-whitesnow/throne).
Запускается как локальный STDIO MCP-сервер и форвардит все вызовы инструментов в
работающий рядом `Throne.Api` (по умолчанию `http://localhost:5008`). Все мутации
и SSE-фанаут остаются в процессе API — UI обновляется by construction
(см. [ADR-0009](https://github.com/gently-whitesnow/throne/blob/main/specs/ADR/0009-cross-process-realtime-fanout.md)).

## Установка

Требуется .NET 10 SDK.

```bash
dotnet tool install -g Throne.Mcp.Stdio
```

Обновление:

```bash
dotnet tool update -g Throne.Mcp.Stdio
```

## Использование

Сначала подними Throne.Api (`docker compose --profile full up` или `dotnet run`
в `apps/api/src/Throne.Api`). После этого пропиши STDIO-сервер в конфиге своего
MCP-клиента:

```json
{
  "mcpServers": {
    "throne": {
      "command": "throne-mcp-stdio"
    }
  }
}
```

Кастомный адрес API — через переменную окружения `THRONE_API_BASE_URL` или
конфигурацию `Throne:ApiBaseUrl`.
