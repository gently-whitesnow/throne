# Generic HTTP Task Tracker Contract

`custom-http` is the built-in adapter for private task systems that expose a small read-only HTTP
contract. The private system stays hidden behind `base_url + service token`; Throne stores only the
connection, selected board coordinates, and non-authoritative card snapshots.

## Auth

Every request uses machine-to-machine bearer auth:

```http
Authorization: Bearer <token>
```

Status mapping follows ADR-0053:

- `401` / `403` -> auth problem.
- `402` -> blocked.
- `5xx`, timeout, transport failure -> offline.
- `404` is only a gone card on `GET /cards/{card_id}`.

## Endpoints

All paths are resolved under `{base_url}/api/task-tracker`.

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/health` | Probe token and reachability. Any 2xx means connected. |
| `GET` | `/boards` | Return selectable board/facet list. |
| `GET` | `/boards/{board_id}/cards` | Return active cards for a board. |
| `GET` | `/boards/{board_id}/cards/search?query=&limit=10` | Return a first page of active cards; empty query means recent-first. |
| `GET` | `/cards/{card_id}` | Return one active card by id; `404` means gone or not readable as active. |

## DTOs

Boards:

```json
{
  "boards": [
    { "board_id": "coding", "title": "Coding tasks" }
  ]
}
```

Cards:

```json
{
  "cards": [
    {
      "card_id": "task-id",
      "board_id": "coding",
      "text": "Task title\n\nFull Markdown text",
      "column_id": null,
      "column_title": null,
      "updated_at": "2026-07-16T10:00:00Z",
      "archived": false,
      "card_version": "opaque-revision",
      "web_url": "https://tracker.example/tasks/task-id"
    }
  ]
}
```

`GET /cards/{card_id}` returns the card object directly. `text` is always required; column fields,
`updated_at`, `card_version`, and `web_url` may be null or omitted. List and search endpoints should
not return closed or archived cards; the adapter also drops rows with `archived = true`.
