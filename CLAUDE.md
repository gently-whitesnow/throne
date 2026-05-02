# CLAUDE.md — Throne

Структура и команды: [readme.md](readme.md). Продуктовая постановка приходит вместе с запросом пользователя — на репозитории её не ищи.

## Перед завершением хода

```bash
bash scripts/quality/verify.sh
```

Должно вернуть `ALL GATES PASSED`. Падает — чини root cause, не отключай гейты.

## Куда смотреть

| Зачем | Где |
|---|---|
| Архитектурные решения | [specs/ADR/REGISTRY.md](specs/ADR/REGISTRY.md) |
| Правила для агентов в этом проекте | [specs/AGENTS.local.md](specs/AGENTS.local.md) |
| HTTP API контракты (OpenAPI source of truth) | [specs/contracts/AGENTS.md](specs/contracts/AGENTS.md) |
| Realtime server→client события (yaml source of truth) | [specs/contracts/realtime/events.yaml](specs/contracts/realtime/events.yaml) + [ADR-0008](specs/ADR/0008-realtime-contract-first-events.md) |
| Seed-инструкции Throne | [apps/api/src/Throne.Application/Instructions/EnsureSeedInstructionsHandler.cs](apps/api/src/Throne.Application/Instructions/EnsureSeedInstructionsHandler.cs) |
| Дизайн-система фронтенда | [DESIGN.md](DESIGN.md) |
| Vendor launchers (Claude/Codex/Cursor) | [specs/ADR/0007-vendor-skill-launchers.md](specs/ADR/0007-vendor-skill-launchers.md) |

## Frontend / UI

Перед любой разработкой `apps/web` или UI-компонентов прочитай [DESIGN.md](DESIGN.md) и используй его как проектную дизайн-систему.

## Что не делать

- Не дублируй сюда содержимое ADR / readme — только ссылки.
- Не описывай тут архитектуру или модели — это ADR.
- Не пиши «недавние изменения» — это `git log`.
- Продуктовая постановка живёт вне репозитория и приходит с запросом пользователя. Не строй предположений из остатков прошлых итераций в коде.
