# CLAUDE.md — Throne

Структура и команды: [readme.md](readme.md). Продуктовая постановка приходит вместе с запросом пользователя — на репозитории её не ищи.

## Перед завершением хода

Гейты декларированы в [.quality/quality.config.json](.quality/quality.config.json). Бегунок — [scripts/quality/verify.py](scripts/quality/verify.py); `verify.sh` оставлен как тонкая обёртка для совместимости.

```bash
bash scripts/quality/verify.sh --fast   # в процессе работы (~1 мин, без integration-тестов и audits)
bash scripts/quality/verify.sh          # перед сдачей хода (полный, включая integration + audits)
bash scripts/quality/verify.sh --list   # перечислить гейты и их статус
```

Падает — чини root cause, не отключай гейты. Ослабление гейта (правка `quality.config.json` enabled, baseline-снимки, или comment в Architecture-тесте) требует rationale в коммите.

## Куда смотреть

| Зачем | Где |
|---|---|
| Архитектурные решения | [specs/ADR/REGISTRY.md](specs/ADR/REGISTRY.md) |
| Правила для агентов в этом проекте | [specs/AGENTS.local.md](specs/AGENTS.local.md) |
| HTTP API контракты (OpenAPI source of truth) | [specs/contracts/AGENTS.md](specs/contracts/AGENTS.md) |
| Realtime server→client события (yaml source of truth) | [specs/contracts/realtime/events.yaml](specs/contracts/realtime/events.yaml) + [ADR-0008](specs/ADR/0008-realtime-contract-first-events.md) |
| System instructions + bundle манифест (источник правды) | [specs/manifest/throne-skills.yaml](specs/manifest/throne-skills.yaml) |
| Дизайн-система фронтенда | [DESIGN.md](DESIGN.md) |
| MCP-доставка инструкций (mini-router в `InitializeResult.instructions`) | [specs/ADR/0014-mcp-initialize-instructions-routing.md](specs/ADR/0014-mcp-initialize-instructions-routing.md) |

## Frontend / UI

Перед любой разработкой `apps/web` или UI-компонентов прочитай [DESIGN.md](DESIGN.md) и используй его как проектную дизайн-систему.

## Что не делать

- Не дублируй сюда содержимое ADR / readme — только ссылки.
- Не описывай тут архитектуру или модели — это ADR.
- Не пиши «недавние изменения» — это `git log`.
- Продуктовая постановка живёт вне репозитория и приходит с запросом пользователя. Не строй предположений из остатков прошлых итераций в коде.
