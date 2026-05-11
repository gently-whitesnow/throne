# ADR-0015: Chat-history transfer через локальный sidecar CLI

Status: Superseded by ADR-0022
Date: 2026-05-09

> **Superseded by ADR-0022 (см. REGISTRY).** Chat-history transfer и серверный приём диалогов убраны в пользу frontier-driven dream flow, где фронтир читает диалоги локально.

## Context

Цикл улучшения Throne (см. [readme.md](../../readme.md), раздел «Миссия») предполагает, что оператор грузит дампы своих диалогов с агентами, а Throne нарезает из них правки к user-инструкциям и подсказки к Intent.text. Пользователь говорит агенту «отправь историю в Трон», и архив должен оказаться на сервере.

Предыдущая редакция этого ADR описывала схему «агент-курьер»: агент сам читал `~/.claude/projects/**` / `~/.codex/sessions/**`, упаковывал zip и слал multipart-upload на `POST /api/v1/chat-uploads` со своим Bearer-токеном. На практике подход не работает на части окружений по двум независимым причинам:

- **У агента не всегда есть Bearer для Throne.** PAT в MCP-конфиге — частный случай. У OAuth-юзеров (а это основной путь для Claude Desktop как Custom Connector через [ADR-0016](0016-mcp-oauth-authorization.md)) валидного токена для произвольного REST-вызова нет — получаем 401 «не по своей вине».
- **Sandbox-политики харнесса агента запрещают выгрузку файлов с машины.** Чтение пользовательских директорий и отправка их наружу как multipart — ровно тот класс действий, который ограничивается. Это политика, а не баг, и обходить её нельзя.

UX-вход «попроси агента — он зальёт» нужно сохранить, но обе ответственности (хранить креды + переносить байты) с агента надо снять.

Действующие ограничения, которые остаются от предыдущей редакции:

- **Доставка инструкций — через MCP-роутер.** [ADR-0014](0014-mcp-initialize-instructions-routing.md) уже прокидывает mini-router в `InitializeResult.instructions`. Mode `transfer` остаётся точкой, где агент узнаёт, что делать.
- **Аутентификация — существующий PAT.** ADR-0012: MCP-клиенты ходят по PAT, веб — по JWT. CLI в MVP принимает только PAT; device-code OAuth — отдельный intent позже.
- **Multi-tenancy.** ADR-0012 требует `OwnerUserId` на user-owned записях; архивы — на user-owned записях по определению.

## Decision

### Локальный sidecar CLI вместо агента-курьера

На машину пользователя ставится маленький Throne-клиент, который сам читает локальные истории и шлёт их в Throne. Агент превращается из курьера в UX-фронт: вызывает CLI через Bash и пересказывает stdout пользователю. Креды и байты идут мимо контекста модели → харнесс не возражает, OAuth-юзеры не упираются в отсутствие Bearer.

### Стек и поставка

- **Node.js + TypeScript**, npm-пакет `@gently-whitesnow/throne-cli`. Кросс-платформенный keychain — `keytar`, OpenAPI-сгенерированный клиент переиспользуется из контракта `chat-uploads` (тот же codegen, что у `apps/web` по [ADR-0006](0006-openapi-contract-first-codegen.md)).
- **Размещение в монорепо** — `apps/cli`. Одной PR двигаем `specs/contracts/chat-uploads/openapi.yaml` + сервер + клиент атомарно; quality-gates `apps/*` накрывают CLI без новой инфраструктуры.
- **Доставка**: основной канал — `npm i -g @gently-whitesnow/throne-cli` и `npx -y @gently-whitesnow/throne-cli sync claude-code` (даёт агенту zero-install запуск). Brew formula — поверх npm как обёртка, отдельным шагом при необходимости.

Альтернативы и почему отклонены:
- *dotnet tool* — требует .NET SDK у пользователя, install friction;
- *Go / Rust single binary* — добавляют новый стек в монорепо ради малого CLI;
- *отдельный репо для CLI* — лишний CI/release pipeline и потеря атомарности контрактных правок.

### Команды CLI (MVP)

- `throne login` — ввод PAT (вручную или `--token`), токен в OS keychain через `keytar`.
- `throne sync claude-code [--since=...] [--dry-run] [--force]` — сканирует Claude Code-сессии в `~/.claude/projects/**`, показывает сводку, грузит дельту.
- `throne sync claude-desktop [...]` — то же для Claude Desktop.
- `throne sync codex-cli [...]` — то же для Codex CLI.
- `throne sync codex-desktop [...]` — то же для Codex Desktop.
- `throne uploads list` — посмотреть, что уже залито.
- `throne daemon start` — фоновый watcher с инкрементальным sync; вне MVP, фаза 2.

Поддержку Cursor/Cline/Aider добавляем адаптером в CLI без правок mini-router'а и system-инструкций.

### Где остаётся агент

Агент через Bash зовёт `throne sync <vendor-id>` с одним из `claude-code`, `claude-desktop`, `codex-cli`, `codex-desktop`. Если CLI не установлен — подсказывает пользователю установку. CLI печатает прогресс в stdout, агент пересказывает результат. `system_instructions[kind: transfer]` в [specs/manifest/throne-skills.yaml](../manifest/throne-skills.yaml) — короткий runbook ровно про это, без чтения локальных директорий и без multipart-upload агентом. Mini-router ([`ThroneServerInstructions.MiniRouter`](../../apps/api/src/Throne.Application/Instructions/ThroneServerInstructions.cs)) — без изменений: строка `send chat history to Throne for training → mode="transfer"` ведёт на новый bundle.

### Гранулярность — одна сессия = одна запись

Каждый файл сессии (например, `~/.claude/projects/<projectId>/<sessionId>.jsonl`) упаковывается отдельным zip и шлётся как отдельная запись `chat_uploads`. `manifest.json` описывает ровно одну сессию: `{vendor, sessionId, projectId, startedAt, lastMessageAt, messageCount}`.

Почему не «месячными пакетами»:
- идемпотентность по `(userId, vendor, sessionId, contentHash)` работает только если `sessionId` — стабильный логический ключ; у monthly-batch hash «текущего месяца» меняется при каждой новой сессии → дубли либо upsert с потерей audit'а;
- дельта-синк становится визуально честным: «была активность в одной сессии — улетел один upload»;
- удаление и просмотр в UI работают на уровне сессии.

Цена — рост числа документов в `chat_uploads` (сотни–тысячи на активного пользователя). Снимается уникальным индексом `(userId, vendor, sessionId, contentHash)` в Mongo и группировкой на фронте (vendor → projectId → сессии, с виртуализацией списка). Альтернатива «hybrid с sealing» отклонена как преждевременная оптимизация.

### Идемпотентность повторных sync

Стратегия — серверная дедупликация как контракт + локальный cache в CLI как оптимизация.

- **Серверный ключ:** `(userId, vendor, sessionId, contentHash)`. Повторная загрузка с тем же ключом — `200 OK` с `deduplicated: true`, новая запись не создаётся, realtime-событие не публикуется.
- **Локальный cache CLI:** `~/.throne/sync-state.json` (per-vendor): `{sessionId, mtime, size, sha256, uploadedAt}`. На запуске CLI сканирует директорию, фильтрует по cache, шлёт только дельту. Cache — не источник правды: если потерян или CLI на другой машине, отправится больше, но сервер дедуплицирует.
- **Флаги:** `--since=<date>` — по mtime файла; `--force` — игнорирует локальный cache (но не серверную дедупликацию); `--dry-run` — показывает дельту без отправки.

### REST API (без архитектурных изменений)

Контракт `specs/contracts/chat-uploads/openapi.yaml`, эндпоинты `POST/GET/DELETE/GET download` под `/api/v1/chat-uploads` — остаются как точка интеграции для CLI. Минимальное расширение под идемпотентность:

- `POST /api/v1/chat-uploads`: добавляются обязательные поля `vendor` (`claude-code` | `claude-desktop` | `codex-cli` | `codex-desktop`), `sessionId`, `contentHash` (sha256 hex). Архив — `multipart/form-data`.
- Response: добавляется `deduplicated: boolean`. Если `true` — запись не создана, существующая возвращается как есть.
- Mongo: уникальный индекс `(userId, vendor, sessionId, contentHash)` с partial filter (только не-soft-deleted).

Аутентификация — общая для модуля: PAT (для CLI и интеграций) и JWT (для UI). PAT-middleware расширяется с `/mcp` на `/api/v1` (предыдущая привязка только к `/mcp` — блокер для CLI; снимается в подзадаче #1).

### Хранилище

Без изменений относительно предыдущей редакции:
- Архивы — на host-volume директории внутри docker-контейнера, путь `ChatUploads:StoragePath` (по умолчанию `/var/lib/throne/chat-uploads/`). Имена файлов — `<id>.zip`. Layout — плоский в MVP.
- Mongo-коллекция `chat_uploads` хранит метаданные: `Id, OwnerUserId, Vendor, SessionId, ProjectId?, ContentHash, AgentVersion?, Device?, DeviceDisplayName?, StartedAt, LastMessageAt, MessageCount, SizeBytes, FilePath, Status, CreatedAt`.
- Не используем GridFS: архивы — самодостаточные blob'ы, файловая система проще для бэкапа и не тащит Mongo-connection в repo-слой.

### Realtime ([ADR-0008](0008-realtime-contract-first-events.md))

`chat_upload.created` (payload — `ChatUploadDto`) и `chat_upload.deleted` (payload — `{ chat_upload_id }`) — без изменений. Декоратор `DomainEventDispatchingUnitOfWork` фанаутит их через `RealtimeDomainEventHandler` стандартным pipeline.

### UI (`apps/web`, FSD)

Страница `/chat-uploads` остаётся read+manage. Никаких форм загрузки в UI — загрузка только через CLI/агента. Колонки: vendor, session, projectId, период, # сообщений, размер, createdAt, действия (download, delete). Группировка vendor → projectId → сессии и виртуализация списка — отдельный intent после фактического роста объёма.

### Manifest schema (внутри zip)

```json
{
  "schemaVersion": 2,
  "vendor": "claude-code",
  "sessionId": "abc123",
  "projectId": "throne",
  "agentVersion": "1.2.3",
  "device": "user@host",
  "deviceDisplayName": "MacBook Pro",
  "createdAt": "2026-05-07T19:41:05Z",
  "startedAt": "2026-04-15T10:00:00Z",
  "lastMessageAt": "2026-04-15T13:30:00Z",
  "messageCount": 42,
  "sizeBytes": 123456,
  "contentHash": "..."
}
```

Schema bumped to `2`: per-session, не batch. Приёмник схему `1` не принимает — старых клиентов нет, переписываем без обратной совместимости.

### Аутентификация в MVP — PAT-only

`throne login` принимает только Personal Access Token. Device-code OAuth flow — отдельный intent после стабилизации sidecar'а. Аргументы:
- сужает MVP до проверяемого минимума, не зависит от готовности device-code эндпоинтов;
- у пользователей с PAT в MCP-конфиге — zero friction (тот же токен);
- OAuth-юзеры временно создают PAT в UI — приемлемая цена за быстрый MVP.

### Конфигурация endpoint в CLI

CLI ищет API URL в порядке: `--api-url` флаг → `THRONE_API_URL` env → `~/.throne/config.json` поле `apiUrl` → встроенный prod-default. Под dev — `THRONE_API_URL=http://localhost:5000`. Без сервера discovery: prod URL хардкодится в пакете при сборке.

### Безопасность / приватность

Без изменений: никакой клиентской/серверной фильтрации секретов в диалогах (явное решение MVP). Один пользователь видит только свои архивы (`OwnerUserId`-фильтр в репозитории). PAT хранится в OS keychain через `keytar`, не в plaintext.

## Consequences

### Positive

- **Снимает оба ограничения предыдущей редакции разом.** Агент не держит Bearer и не выгружает файлы — sandbox-политики и отсутствие OAuth-токена больше не блокируют UX «попроси агента, он зальёт».
- **Инкрементальный sync без раздувания контекста агента.** CLI сам помнит, что уже отправлено; агент видит только итоговую сводку.
- **Расширение на новых вендоров — это адаптер в CLI**, не правка mini-router'а или system-инструкций.
- **Контракт остаётся атомарным.** `apps/cli` в монорепо двигает `openapi.yaml` + сервер + клиент одной PR.
- **Идемпотентность встроена в контракт**, а не в клиента — две машины и `--force` не плодят дубли.

### Negative / Risks

- **Install friction.** Пользователь обязан иметь Node.js (или принять `npx -y @gently-whitesnow/throne-cli`). Для не-разработчиков это ненулевой шаг; митигируем доками и `npx`-вызовом из агента.
- **Manifest стал контрактом v2.** Любое будущее изменение схемы потребует `schemaVersion ≥ 3` + обратной совместимости приёма.
- **Файлы на диске стоят отдельно от метаданных Mongo.** Атомарность «удалили запись + удалили файл» — best-effort, как и раньше; принимаем риск ручного cleanup при падении между шагами.
- **Без фильтрации секретов растёт ответственность пользователя.** В диалогах могут оказаться API-ключи; MVP осознанно не предупреждает в UI.
- **Resumable upload не делаем.** Большой архив с разрывом сети пересылается целиком. Гранулярность «одна сессия = один upload» делает проблему меньше, но не убирает.
- **Per-session документы в `chat_uploads` растут быстрее**, чем в monthly-batch. Снимается уникальным индексом и виртуализацией UI; лимит роста — пересмотрим, если станет жалобой.

## История

Предыдущая редакция этого ADR описывала схему «агент-курьер»: агент сам читал `~/.claude/projects/**`, упаковывал zip и слал multipart-upload через MCP-mode `transfer` + REST. Подход не работал на части окружений из-за двух независимых ограничений — sandbox-политики харнесса агента, запрещающие выгрузку файлов с машины через инструменты модели, и отсутствие Bearer-токена у OAuth-юзеров. Переписано на sidecar CLI: агент остаётся UX-фронтом, но креды и байты идут мимо его контекста. Запись в [REGISTRY.md](REGISTRY.md) сохраняет номер 0015; родительская постановка переписки — intent `a4b704bdc0e14c229f625a6556fe7d36`.
