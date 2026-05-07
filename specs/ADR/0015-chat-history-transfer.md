# ADR-0015: Chat-history transfer (добровольный сбор переписок с агентов)

## Status

Accepted

## Context

Цикл улучшения Throne (см. [readme.md](../../readme.md), раздел «Миссия») предполагает, что оператор грузит дампы своих диалогов с агентами, а Throne нарезает из них правки к user-инструкциям и подсказки к Intent.text. Раньше этот шаг существовал только в форме идеи. Нужно дать пользователю первый практический канал: загружать архивы переписок (Claude Code, Claude Desktop, Codex CLI и их вариации) в Throne своими руками — точнее, руками агента, которого он попросит «отправь историю в Трон».

Ключевые ограничения постановки:

- **Запуск из чата с агентом, а не из UI.** Загрузку инициирует агент: он сам знает раскладку файлов своего вендора и может посчитать `device_id`, отчитаться сводкой и дождаться апрува пользователя. UI-форма загрузки в MVP избыточна и плодит вопросов про путь, формат, скрытые файлы.
- **Доставка инструкций — через существующий MCP-роутер.** ADR-0014 уже прокидывает mini-router в `InitializeResult.instructions`. Один абзац в роутере + новый mode превращают поток «пользователь сказал → агент выбрал mode → сервер вернул bundle» в стандартный путь, без новых каналов доставки.
- **Хранение — фактический файл архива.** Архивы непригодны как «структурированный документ Mongo»: они большие (десятки/сотни мегабайт), redact-ить их сейчас не нужно (ADR явно отказывается от фильтрации секретов в MVP), и они уже самодостаточны как zip + manifest. Mongo GridFS пробовали для intent-аттачей и для них это OK, но архивы переписок — это «контент-объекты», не «вложения сущности», и никаких выгод GridFS не даёт; зато он усложняет бэкап и масштабирование.
- **Авторизация — существующий PAT/JWT.** Уже есть [ADR-0012](0012-throne-behind-auth-gate.md): MCP-клиенты ходят по PAT, веб — по JWT. Дополнительный ephemeral-канал «MCP выдал upload-token» добавляет ровно один новый failure mode (TTL, отзыв, синхронизация секретов) и ноль ценности.
- **Multi-tenancy.** ADR-0012 требует поле `OwnerUserId` на user-owned записях; архивы — на user-owned записях по определению.

## Decision

### Маршрутизация в Throne (новый MCP mode `transfer`)

1. В [specs/manifest/throne-skills.yaml](../manifest/throne-skills.yaml) добавляется:
   - `system_instructions[kind: transfer]` — runbook (см. ниже),
   - `bundles[mode: transfer]` со стандартным набором includes:
     `system: common`, `system: transfer`, `user: common`, `user: transfer`.

2. В mini-router'е [`ThroneServerInstructions.MiniRouter`](../../apps/api/src/Throne.Application/Instructions/ThroneServerInstructions.cs) — одна дополнительная строка:

   `send chat history to Throne for training → mode="transfer"`.

3. Bundle resolver не меняется — `mode → required kinds` остаётся декларативной таблицей.

### Runbook (`system_instructions[kind: transfer]`)

Текст обязан содержать:

- Пути по умолчанию: `~/.claude/projects/<slug>/*.jsonl`, `~/.codex/sessions/...` (расширяется по мере появления вариантов).
- Формула `device_id = username@hostname` (через `os.userInfo().username` + `os.hostname()`); опциональный человекочитаемый `deviceDisplayName`.
- Схема `manifest.json` (см. ниже) с примером.
- Upload endpoint, лимит **200 MB** на архив, инструкция дробить выборку по календарным месяцам (далее по неделям/дням) при превышении; каждый POST = отдельный upload-record в UI.
- UX-протокол:
  1) детектить тип агента (`claude-code`/`claude-desktop`/`codex-cli`/...),
  2) сканировать историю **своего** вендора в первую очередь,
  3) показать сводку «N диалогов, период X..Y, размер Z MB» и ждать явного апрува (всё / часть по ID или диапазону дат),
  4) собрать zip, грузить multipart POST'ом, доложить пользователю,
  5) **после** успешной загрузки своего вендора явно предложить «также загрузить из <другого вендора>».

### REST API (модуль `chat-uploads`, контракт-первый по [ADR-0006](0006-openapi-contract-first-codegen.md))

- Контракт: `specs/contracts/chat-uploads/openapi.yaml`.
- Эндпоинты:
  - `POST /api/v1/chat-uploads` — multipart (поля `archive` zip + `manifest` JSON); возвращает `{id, device, agent, dateRange, conversationCount, sizeBytes, status, createdAt, deviceDisplayName?, agentVersion?}`.
  - `GET /api/v1/chat-uploads` — список архивов текущего пользователя.
  - `DELETE /api/v1/chat-uploads/{id}` — жёсткое удаление файла с диска и записи в Mongo.
  - `GET /api/v1/chat-uploads/{id}/download` — отдать zip обратно.
- Аутентификация — общая для модуля: PAT (для интеграций мимо MCP) и JWT (для UI). PAT даёт полный доступ ко всем 4 эндпоинтам без scope-ограничений.

### Хранилище

- Архивы лежат на смонтированной host-volume директории внутри docker-контейнера, путь — `ChatUploads:StoragePath` в конфигурации (по умолчанию `/var/lib/throne/chat-uploads/`). Имена файлов — `<id>.zip`. Layout — плоский в MVP.
- Mongo-коллекция `chat_uploads` хранит метаданные: `Id, OwnerUserId, Agent, AgentVersion?, Device, DeviceDisplayName?, DateRangeFrom, DateRangeTo, ConversationCount, SizeBytes, FilePath, Status, CreatedAt`.
- Доступ к архивам в репозитории фильтруется по `owner_user_id` + `id`. На приёме сервер валидирует: `manifest.schemaVersion`, размер архива (≤200 MB), и `sha256` каждого объявленного диалога — расходится с реальным внутри zip → 422.
- **Не используем GridFS.** Архивы — самодостаточные blob'ы, файловая система проще для бэкапа/обслуживания, и repo-слой не должен иметь доступ к Mongo connection ради чтения файла.

### Дедупликация

- В MVP **не делаем** дедуп per-conversation на этапе приёма — каждый upload принимается «как есть». Один upload-record = одна единица в UI.
- Per-conversation `sha256` уже есть в манифесте и используется backend-анализатором, который позже нарежет диалоги на инсайты и сам отбросит дубли.
- «Устройство + дата» из исходной постановки реализуется как **группировка для отображения**, а не первичный ключ. Параллельная работа на двух машинах в один день = два разных архива, оба видны.

### Realtime ([ADR-0008](0008-realtime-contract-first-events.md))

- В [specs/contracts/realtime/events.yaml](../contracts/realtime/events.yaml) добавляются:
  - `chat_upload.created` (payload — `ChatUploadDto`),
  - `chat_upload.deleted` (payload — `{ chat_upload_id }`).
- Mongo-репозиторий несёт events на outcome'ах (`AddOutcome` / `DeleteOutcome`); декоратор `DomainEventDispatchingUnitOfWork` фанаутит их через `RealtimeDomainEventHandler` стандартным pipeline.

### UI (`apps/web`, FSD)

- Новая страница верхнего уровня `/chat-uploads`.
- Структура: `pages/chat-uploads/`, `widgets/chat-uploads-list/`, `entities/chat-upload/`.
- Колонки списка: device, agent, период от..до, # диалогов, размер, загружено (createdAt), действия (download, delete).
- Никаких форм загрузки в UI — read+manage. Загрузка только из чата с агентом.

### Безопасность / приватность

- Никакой клиентской/серверной фильтрации секретов в диалогах (явное решение MVP). UI не предупреждает пользователя про секреты.
- Один пользователь видит только свои архивы (`owner_user_id` фильтр в репозитории).

### Manifest schema (внутри zip)

```json
{
  "schemaVersion": 1,
  "agent": "claude-code",
  "agentVersion": "1.2.3",
  "device": "gently@MacBook-Pro",
  "deviceDisplayName": "MacBook Pro",
  "createdAt": "2026-05-07T19:41:05Z",
  "dateRange": { "from": "2026-04-01T08:00:00Z", "to": "2026-05-07T19:00:00Z" },
  "conversations": [
    {
      "id": "claude-code-abc123",
      "path": "projects/throne/abc123.jsonl",
      "sha256": "...",
      "messageCount": 42,
      "from": "2026-04-15T10:00:00Z",
      "to":   "2026-04-15T13:30:00Z",
      "sizeBytes": 123456
    }
  ]
}
```

## Consequences

### Positive

- **Загрузка появляется без новых каналов доставки и без installer-а.** Mini-router уже на месте, добавляется один абзац + один mode; UI получает новую страницу, но не новый AuthN-флоу.
- **Хранилище предсказуемо.** Архивы — это файлы, и они лежат как файлы. Бэкап сводится к «снять host-volume + dump чистой Mongo-коллекции».
- **Pipeline анализа открыт.** Метаданные + sha256 диалогов в manifest позволяют backend-анализатору позже нарезать инсайты, сохранять их и при этом удалять исходники по запросу пользователя без потери уже извлечённого знания.
- **Расширение на новых вендоров — это поправка runbook'а + допустимое значение `agent`.** Не требует ни новых эндпоинтов, ни архитектурных решений.

### Negative / Risks

- **Manifest становится контрактом, а не свободной формой.** Любое изменение схемы потребует `schemaVersion` ≥2 + обратной совместимости приёма; приёмник придётся учить читать оба формата.
- **Файлы на диске стоят отдельно от метаданных Mongo.** Атомарность «удалили запись + удалили файл» — best-effort: при падении между шагами возможны «осиротевшие» файлы или записи. Принимаем риск (ручной cleanup), не вводим двухфазный коммит.
- **Без фильтрации секретов растёт ответственность пользователя.** В диалогах могут оказаться API-ключи, токены и т.п. MVP делает осознанный выбор не предупреждать в UI; решение пересматриваем, если станет жалобой.
- **Лимит 200 MB.** В оффлайн-зимовке у активного оператора месяц переписки реально превышает лимит. Дробление по месяцам/неделям описано в runbook'е, но это работа агента — увеличивает шанс «недогруженных» периодов при ручном выборе.
- **Resumable upload не делаем.** Большой архив с разрывом сети придётся пересылать целиком. Если станет реальной болью — добавим ADR на chunked upload отдельно, сейчас экономим.
