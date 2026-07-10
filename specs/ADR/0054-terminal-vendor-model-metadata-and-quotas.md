# ADR-0054: Vendor model metadata + best-effort квоты Pro/Max

## Status

Accepted
Date: 2026-07-10
Related: [ADR-0026](0026-embedded-terminal-capabilities-and-run-preflight.md), [ADR-0029](0029-local-first-invariant-and-legacy-auth.md), [ADR-0041](0041-per-intent-terminal-launch-axis.md), [ADR-0042](0042-opencode-shared-serve-and-attach-front.md), [ADR-0045](0045-throne-extension-pattern.md)

## Context

Списки моделей `claude`/`codex` жили инлайном в `TerminalVendorDescriptors.cs` (`Models: ["opus", "sonnet", "haiku"]` и `Models: ["gpt-5.5", "gpt-5.4", "gpt-5.3-codex"]`) — релиз новой модели у вендора требовал править .cs и катить Throne. Одновременно у оператора с Pro/Max-подпиской перед запуском нет способа увидеть, не сожжёт ли следующий запуск 5-часовое или недельное окно квоты.

Соблазн для первого — «сходить live через OAuth `/v1/models` от имени CLI-подписки» — рассыпался при верификации:

- Anthropic ToS (февраль 2026) ограничивает OAuth-токены Claude Code исключительно Claude Code / claude.ai. Использование того же токена сторонним локальным бэкендом Throne — потенциальное нарушение, даже при single-operator local-first инварианте ([ADR-0029](0029-local-first-invariant-and-legacy-auth.md)).
- Community-верификация показывает, что даже когда `api.anthropic.com/v1/models` отвечает под OAuth-токеном, он возвращает **полный публичный каталог**, а не подписочные права аккаунта — «показать реальные права» не решается.
- У Codex CLI никакого live-`/v1/models` вовсе нет: список моделей вкомпилирован в бинарь (`codex-rs/core/src/model_family.rs`), а запросы идут в `chatgpt.com/backend-api/codex/responses` без предварительной инвентаризации.

Для квот, наоборот, точки known-good нашлись: обе CLI-подписки регулярно опрашивают недокументированные HTTP-эндпоинты со своей же OAuth-сессии, и структура ответа стабильна на текущих релизах (Claude Code ≥ 2.1, Codex CLI на 60-секундном polling).

## Subdomain classification

Supporting. Модельный каталог и surfacing квот — прикладная UX-задача над готовой осью запуска ([ADR-0041](0041-per-intent-terminal-launch-axis.md)), core-домена интентов не касается. Обе части следуют per-vendor pluggable pattern из [ADR-0045](0045-throne-extension-pattern.md) (порт → DI fan-out → registry).

## Volatility check

Essential. Обновление модельного каталога — реальный внешний триггер (релиз-цикл вендора). Квоты — операторская наблюдаемость. Ни то, ни другое не давление харнеса.

## Decision

### 1. Модельный каталог — versioned metadata-файл в репо, не live-endpoint

Списки моделей `claude`/`codex` вынесены в `apps/api/src/Throne.Application/Terminals/vendor-models.json` (embedded resource). Дескрипторы читают их через `VendorModelMetadataLoader` на старте процесса. `ModelSource` для этих вендоров остаётся `static` — данные всё ещё хардкод в репо, просто как data-файл рядом с ADR-ми, не как inline-массив в .cs. Порядок в JSON = native-default-first (сохраняем `DefaultModel = Models[0]` семантику).

Обновление каталога = PR, правящий один JSON. Никаких рантайм-миграций, никаких `/v1/models`-шагов на CI, никаких OAuth-токенов в CI.

`opencode` ничего не меняет — остаётся `ModelSourceLocal` с live discovery по `Throne:LocalModel:BaseUrl` из [ADR-0042](0042-opencode-shared-serve-and-attach-front.md).

### 2. Квоты — per-vendor best-effort адаптер с изолированным падением

Новый порт `IVendorQuotaAdapter` в Application, реализации в Infrastructure:

- **Claude**: `GET https://api.anthropic.com/api/oauth/usage` + `Authorization: Bearer <token>` + `anthropic-beta: oauth-2025-04-20`. Токен читается из `~/.claude/.credentials.json` (`claudeAiOauth.accessToken`); на macOS этот файл — fallback хранилища, ключ CLI держит в Keychain, но локальный fallback работает и совместим с SSH-сценариями (см. официальные auth-docs Claude Code). Ответ: `five_hour.used_percentage` / `seven_day.used_percentage` + ISO `resets_at`.
- **Codex**: `GET https://chatgpt.com/backend-api/wham/usage` + `Authorization: Bearer <access_token>` + `ChatGPT-Account-Id: <account_id>` + `originator: codex_cli_rs`. Токен и account_id читаются из `~/.codex/auth.json` (`tokens.access_token`, `tokens.account_id`). Ответ: `RateLimitSnapshot { primary { used_percent, window_minutes, resets_at }, secondary, credits }`.

Оба эндпоинта — недокументированные, ровно те, что дёргают сами CLI (см. `codex-rs/backend-client/src/client.rs::get_rate_limits`, [issue #10869](https://github.com/openai/codex/issues/10869); для Claude — `ohugonnot/claude-code-statusline`). Ломкость известна и принята: любая ошибка адаптера (файл токена отсутствует, expired, 4xx/5xx, неожиданная схема, network-fail) даёт `null` — блок квот в UI просто скрывается для этого вендора, запуск НЕ блокируется. Логируется warning с типом ошибки без токен-контента.

Обновление токенов на нашей стороне НЕ делаем: `access_token` живёт часами (Claude ~8ч, Codex — по `id_token` JWT); стал expired — вернётся 401, адаптер отдаст `null`, обновит токен сам CLI при ближайшем использовании, следующий refetch каталога покажет квоту снова. Reuse rotated refresh-token — единственное использование, а токен-хранилище общее с живым CLI: держаться подальше от refresh — правильный дефолт.

Каждый адаптер обёрнут в single try/catch на всё тело; кэш ответа per-vendor в памяти 60 секунд (совпадает с polling-cadence Codex CLI, не жжёт эндпоинт при частых панель-open/refetch).

### 3. Контракт и UI

`TerminalVendorMetadataDto` получает опциональное поле `quota: TerminalVendorQuotaDto` со схемой:

```
TerminalVendorQuotaDto:
  five_hour:  QuotaWindowDto { used_percent: number, resets_at: string?, window_label: string }
  seven_day:  QuotaWindowDto?
  credits_balance: number?
```

Пусто (`quota: null`) — секция в UI скрыта, никаких «пока грузится». UI под селектором модели рисует компактный прогресс-блок с двумя окнами; при `used_percent >= 80` — предупреждающий стиль, запуск НЕ блокируется (в интенте: «истёк 5h, но weekly ещё есть → только предупреждение»).

Панель дёргает свежий catalog fetch при открытии (invalidate query key) и через кнопку Refresh рядом с селектором модели.

## Consequences

### Positive

- Каталог моделей обновляется PR-ом на один JSON — не правкой `.cs` и не релизным циклом.
- Дескрипторы `TerminalVendorDescriptors.cs` становятся thin: bind-args и флаги, без inline-списков.
- Отсутствуют network-калы на Anthropic API за списком моделей — нулевой ToS-риск и никаких дневных дрейфов.
- Оператор видит расход квоты перед запуском без входа в CLI и без сторонних statusline-скриптов.
- Каждый адаптер изолирован — поломка одного вендора не рушит catalog endpoint и не блокирует запуск другого.

### Negative / Risks

- Квота-эндпоинты недокументированы и без SLA стабильности — на любом релизе Claude Code / Codex CLI ломкость может отвалиться. Смягчено: null-fallback, warning-лог, тесты фиксируют текущую схему, PR по факту поломки — правка адаптера, не всего слайса.
- Модельный JSON — руками поддерживаемый список. Пропущенный релиз вендора = оператор не увидит новую модель до соответствующего PR. Принято сознательно: цена «низковата, чтобы автоматизировать через ToS-серую зону».
- Читаем токен-файлы CLI, но НЕ пишем и НЕ refresh-им — если оператор поменяет схему хранения или Anthropic перейдёт на исключительно Keychain, adapter отдаст null и это ожидаемое поведение.
- Weekly-квота Claude отдаётся только Pro/Max-подпиской и только после первого API-response аккаунта — у свежих аккаунтов блок будет пустой первое время.
