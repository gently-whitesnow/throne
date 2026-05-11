# ADR-0016: OAuth 2.1 на MCP-эндпоинте Throne

## Status

Proposed

## Scope

Throne — open-source. Этот ADR описывает поверхность OAuth 2.1, **которую держит сам
Throne**: какие эндпоинты публикует, какие токены принимает, по каким правилам
валидирует. Конкретный Authorization Server — деталь деплоя; throne общается с ним
только через стандартные RFC-механизмы (OIDC discovery, JWKS). Любая обвязка,
реализующая RFC 8414 / RFC 9728 / OIDC discovery и выпускающая RS256 JWT с
`sub = throne-userId`, годится без правок кода throne.

## Context

Сейчас `/mcp` авторизуется только статическим Personal Access Token
(`Authorization: Bearer tpat_…`). Этого хватает для CLI/скриптов, но ограничивает
каналы доставки:

- **Claude Desktop с PAT** подключается только через bridge `npx mcp-remote` как
  STDIO-сервер. Bridge **не пробрасывает** `InitializeResult.instructions` от
  апстрима к клиенту — mini-router из [ADR-0014](0014-mcp-initialize-instructions-routing.md)
  до агента не доходит. Подтверждено: live-curl на прод отдаёт `instructions` в
  ответе на `initialize`, при этом блока `## throne` в системном промпте Claude
  Desktop нет.
- **Claude Desktop «Custom Connector»** — нативный remote-MCP-канал, который
  доставляет `instructions` корректно — поддерживает в форме только
  OAuth Client ID/Secret. Поля для статического Bearer там нет.
- Любые сторонние MCP-клиенты, поддерживающие только OAuth (MCP authorization-spec
  предполагает OAuth 2.1 как канонический путь), сегодня Throne подключить не могут.

Цель — поднять OAuth 2.1 на MCP-эндпоинте, чтобы Throne добавлялся в Claude
Desktop как Custom Connector штатным путём, а mini-router из
`InitializeResult.instructions` гарантированно долетал до агента.

## Decision

### Throne — resource server, не Authorization Server

Throne не выпускает токены и не хранит OAuth-клиентов. Эта функциональность
живёт во внешнем Authorization Server'е, который выбирается деплойментом
(собственный AS, любой OIDC-совместимый OSS, managed-сервис). Throne знает про
этот AS только две вещи:

- его OIDC discovery URL (конфиг `Auth:Authority`),
- его JWKS (подтягивается автоматически из `{Authority}/.well-known/openid-configuration`).

Никаких throne-specific dependencies или клея — только стандартные RFC-механизмы.

### Что throne публикует

- `GET /.well-known/oauth-protected-resource` (RFC 9728) — указывает на AS как
  authorization_servers, перечисляет поддерживаемые scope'ы и методы доставки
  Bearer'а. Контент собирается из `AuthOptions` без зашитых URL.
- `/mcp` принимает Bearer одного из двух типов:
  - **PAT** (`tpat_…`) — текущий канал, без изменений (PAT остаётся, см. явный
    non-goal). Маршрутизация определяется по shape: PAT не содержит точек,
    JWT — три base64url-сегмента, разделённых точками.
  - **OAuth access_token** — JWT (RS256), выпущенный внешним AS. Валидируется
    локально по JWKS (`/.well-known/jwks.json` AS), без походов на introspection.

### Контракт токена

Throne принимает access_token, удовлетворяющий следующему профилю:

- формат — JWS-compact JWT (RFC 7519), алгоритм `RS256`;
- `iss` = `Auth:Authority` (как настроен в throne);
- `aud` ∈ `Auth:AdditionalAudiences` — каждое значение совпадает с публичным
  URL соответствующего MCP-эндпоинта (например, `https://example.org/mcp`);
- `sub` — стабильный идентификатор пользователя; маппится напрямую в
  `OwnerUserId`. **Ровно тот же subject**, что AS выдаёт для REST-сессий, —
  Throne не делает разницы между OAuth и REST-каналами на уровне идентичности.
- `exp` — обязательный.

Дополнительные claim'ы (`scope`, `client_id`, `azp`) допустимы; throne их не
интерпретирует в MVP (scope `mcp:full` подразумевается единственным).

### Поведение клиента (Custom Connector pathway)

1. Пользователь добавляет throne как Custom Connector с URL вида
   `https://<host>/mcp`.
2. Клиент делает discovery `/.well-known/oauth-protected-resource` → находит AS.
3. Клиент регистрируется через DCR (на стороне AS, не throne), ведёт пользователя
   через `/authorize`, получает `access_token` через `/token`. Все три эндпоинта —
   на AS, throne про их URL не знает.
4. Клиент вызывает `initialize` с `Authorization: Bearer <access_token>` —
   throne проверяет JWT по JWKS, отдаёт `InitializeResult.instructions`
   (mini-router ADR-0014).
5. PAT-канал продолжает работать параллельно для CLI/скриптов.

## Что НЕ в скоупе MVP

- Замена PAT. PAT остаётся как альтернатива.
- Scope-разделение прав (PAT сейчас полный доступ — стартуем с тем же
  `mcp:full`).
- Throne-side админка OAuth-клиентов / consent-историй — это всё функции AS.

## Consequences

### Positive

- Throne остаётся пригодным к open-source без эффектов: код не упоминает ни
  один конкретный AS, дефолтная конфигурация работает с любым OIDC-совместимым
  сервером, который умеет RS256.
- Claude Desktop подключается штатным каналом «Custom Connector»; mini-router
  из ADR-0014 гарантированно долетает до агента.
- Совместимость с любыми сторонними MCP-клиентами, поддерживающими OAuth.

### Negative / Risks

- Без bound-aware валидации scope'ов (`mcp:read`/`mcp:write`/`mcp:dream`)
  любой выпущенный access_token имеет полный доступ — то же ограничение, что
  у PAT сегодня. Митигация — отдельная работа на v2 со scope-моделью.
- Throne делегирует AS не только аутентификацию, но и lifecycle (revoke,
  rotation). При компрометации refresh-токена восстановление доступа целиком
  зависит от инструментария AS.

## Implementation references

- `apps/api/src/Throne.Api/Auth/AuthOptions.cs` — `Authority`, `Audience`,
  `AdditionalAudiences`. Конкретный AS не упомянут.
- `apps/api/src/Throne.Api/Auth/PersonalAccessTokenMcpMiddleware.cs` —
  ветка JWT-shaped токенов в JwtBearer, иначе PAT.
- `apps/api/src/Throne.Api/Auth/ProtectedResourceMetadataEndpoint.cs` — RFC 9728.
