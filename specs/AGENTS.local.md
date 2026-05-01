# AGENTS.local — Throne project specifics

Дополняет [USER.md](../USER.md) проектными правилами.

## Перед завершением хода

```bash
bash scripts/quality/verify.sh
```

Должно вернуть PASS. Чинить root cause, не обходить гейты.

## Архитектурные слои (apps/api)

Зависимости — строго внутрь:

```
Api ──► Application ──► Domain
Infrastructure ──► Application ──► Domain
Api ──► Infrastructure (только в Program.cs / DI wiring)
```

- **Throne.Domain** — entities, value objects, доменные правила. Без внешних зависимостей.
- **Throne.Application** — use cases и порты (`IIntentRepository`, `IInstructionRepository`). Не знает про MongoDB и MCP.
- **Throne.Infrastructure** — реализация портов (Mongo).
- **Throne.Api** — composition root + транспорт. Сейчас MCP, в будущем HTTP для `apps/web`.

Нарушение направления зависимостей провалит `Throne.Architecture.Tests`.

## Frontend / UI

При работе над `apps/web` или UI-компонентами используй [DESIGN.md](../DESIGN.md) как источник проектной дизайн-системы.

## Изменения, требующие ADR

- Смена архитектурного стиля или layout слоёв.
- Замена storage / транспорта.
- Включение нового quality pack (coverage, mutation, и т.п.).

Шаблон ADR: [specs/ADR/.template.md](ADR/.template.md). После добавления — обнови [specs/ADR/REGISTRY.md](ADR/REGISTRY.md).

## Постановка задачи

Продуктовая постановка приходит вместе с запросом пользователя (например, как приложенный документ или текст в сообщении). В репозитории её не хранится. Не реконструируй намерение из остатков прошлых итераций в коде — спроси, если запрос неполный.
