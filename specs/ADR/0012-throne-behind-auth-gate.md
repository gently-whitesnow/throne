# ADR-0012: Throne behind auth-gate — JWT consumption and MCP Personal Access Token

## Status

Proposed

## Context

Throne должен быть пригоден к деплою на публичный сервер без того, чтобы превращаться
в identity provider. Авторизация выносится в отдельный сервис **auth-gate** (отдельный
intent/repo): nginx + auth-api (.NET) с провайдерами Telegram/Google, JWKS, RS256/ES256;
в claims — только внутренний `user_id`. Throne стоит за nginx auth-gate как обычный
backend.

Возникающие вопросы и ограничения:

- Throne не должен знать про Telegram, HMAC, BotToken, login UI, refresh tokens, cookie.
- MCP-клиенты (Claude Code / Codex / Cursor) не умеют интерактивный refresh-flow.
- Локальная разработка должна продолжать работать без живого auth-gate и без миграции
  данных.
- Multi-user разделение данных требует атрибуции каждого записанного в Mongo артефакта
  владельцу.

Альтернативы, которые отклонены:

- Сделать auth внутри Throne (`88f2cf03d6bb4793b47971217d38f9ca`). Велосипед, плохая
  переиспользуемость для других приложений за тем же auth-gate.
- Проксировать MCP через access-jwt с коротким TTL: MCP-клиенты не умеют refresh-flow,
  так что выбран PAT.

## Decision

### JWT consumption (web и API)

- Стандартный middleware `AddJwtBearer` с `Authority = auth-gate` (или `MetadataAddress`
  на `/.well-known/jwks.json`). JWKS кэшируется автоматически.
- Issuer и audience берутся из конфига (`JWT_ISSUER`, `JWT_AUDIENCE`).
- Из claims читается только внутренний `user_id` и попадает в ambient
  `ICurrentUserAccessor`. Без валидного токена — 401.
- Никаких cookie, refresh, login-endpoint в Throne.

### MCP authentication — Personal Access Token

- Пользователь, авторизованный через auth-gate в web-UI, генерирует PAT на странице
  «MCP Token». Один активный PAT на пользователя; перегенерация инвалидирует предыдущий.
  TTL — бесконечный.
- Хранится в Mongo как SHA-256 хеш, сравнивается constant-time. Сам секрет показывается
  пользователю один раз (copy-once).
- Endpoints: `POST /v1/me/mcp-token`, `GET /v1/me/mcp-token`.
- MCP middleware читает `Authorization: Bearer <token>` (или `?token=`), резолвит userId,
  кладёт в ambient `ICurrentUserAccessor`. Без токена — 401.
- PAT — собственный непрозрачный токен Throne, не JWT auth-gate. Причина: auth-gate
  выдаёт только короткие access-jwt + refresh-cookie, а MCP-клиент не умеет ходить за
  refresh.

### Multi-user data separation

- Поле `OwnerUserId` на user-owned агрегатах: `Intent`, `Instruction(scope=user)`,
  `IntentQa`, `IntentReview`, `DreamRun`, `IntentAttachment`, `mcp_call_log`,
  `PersonalAccessToken`.
- Все репозитории фильтруют выборки по `OwnerUserId`.
- Видимость строго приватная. Sharing/ACL/workspaces — вне MVP.
- Открытая регистрация: первый успешный логин в auth-gate создаёт `user`. Throne узнаёт
  о пользователе при первом запросе с его `user_id` — никаких локальных таблиц
  `users`/`external_logins` в Throne.

### Local development

- Конфиг `Auth:Mode ∈ {Jwt, Disabled}`.
- При `Disabled` JWT/PAT middleware подмешивает `userId="local-dev"` в HTTP-контекст
  и пропускает запрос. Один codepath, без раздвоения dev↔prod.
- Существующая локальная БД продолжает работать; миграции данных в dev не нужны.

### Audit log

- Существующая коллекция `mcp_call_log` (ADR-0004) расширяется полем `user_id`.
  Старые записи остаются с `null`/`"local-dev"`.

## Roll-out

Реализация делится на инкрементальные шаги, каждый из которых компилируется и проходит
quality gates. Этот ADR фиксирует целевую архитектуру; не все шаги выполняются за один
проход.

1. **Foundation (этот шаг).** Введён порт `Throne.Application.Auth.ICurrentUserAccessor`
   и константа `CurrentUserIds.LocalDev`. В `Throne.Api.Auth` появились `AuthOptions`
   (`Mode = Disabled` по умолчанию), `LocalDevCurrentUserAccessor`, `AuthServices`
   bootstrap. `Mode = Jwt` пока бросает `NotSupportedException` — настоящий JWT
   middleware подключается отдельным шагом. `McpCallLogEntry` и `mcp_call_log`
   расширены полем `userId`; AuditingMcpServerTool/Prompt пишут его из аккессора.
2. `OwnerUserId` на user-owned агрегатах + миграция Mongo + architecture-test
   (handler не пишет user-owned-сущность без `OwnerUserId`).
3. JWT middleware (`Auth:Mode = Jwt`) с маппингом `user_id` claim → ambient.
4. PAT: коллекция `personal_access_tokens`, endpoints, web-страница «MCP Token»,
   MCP middleware.
5. Документация и smoke-тест против реального auth-gate.

## Consequences

### Positive

- Throne полностью развязан с identity-провайдерами. Тот же auth-gate переиспользуется
  для других приложений без изменений в Throne.
- MCP-клиенты получают понятный долгоживущий механизм авторизации (PAT) без интерактива.
- Один codepath dev/prod через `Auth:Mode`. Локальная разработка не ломается.
- Audit log приобретает атрибуцию по пользователю; данные с самого начала готовы к
  multi-user.

### Negative / Risks

- PAT — bearer-секрет с бесконечным TTL. Compromised token выдаёт полный доступ до
  ручной перегенерации. Митигация: один активный PAT на пользователя, SHA-256 хеш в БД,
  copy-once UI, ручная перегенерация.
- `OwnerUserId` нужно протащить через все handler'ы; пропущенный handler — потенциальная
  data leak. Митигация: architecture-тест и репозиторный фильтр.
- Auth:Mode=Jwt пока не реализован, но уже в enum'е. До появления настоящей реализации
  включение режима в продакшне приводит к startup failure (намеренно — fail-fast).
