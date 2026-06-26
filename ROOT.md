# ROOT.md — Throne agent entry

Канон общих правил для агентов в этом репозитории. `CLAUDE.md` и `AGENTS.md` — тонкие vendor-стабы со ссылкой сюда.

Структура и команды: [readme.md](readme.md). Продуктовая постановка приходит вместе с запросом пользователя — на репозитории её не ищи и не реконструируй из остатков прошлых итераций в коде.

## Перед завершением хода

Гейты декларированы в [.quality/quality.config.json](.quality/quality.config.json), бегунок — [scripts/quality/verify.py](scripts/quality/verify.py); `verify.sh` оставлен как тонкая обёртка для совместимости. Полный перечень команд (`--fast` / `--list` / `--only` / `--skip` / `--scope`) — в [specs/AGENTS.local.md](specs/AGENTS.local.md#перед-завершением-хода).

Падает — чини root cause, не отключай гейты. Ослабление гейта (правка `quality.config.json` enabled, baseline-снимки, или comment в Architecture-тесте) требует rationale в коммите.

## Куда смотреть

Таблица навигации по проекту — [readme.md → «Где что искать»](readme.md#где-что-искать). Проектные правила для агентов — [specs/AGENTS.local.md](specs/AGENTS.local.md).

## Поднять свой throne для дебага

Throne теперь один процесс (UI+API+SQLite), поэтому в сессии можно поднять изолированный инстанс и продебажить сделанное, не трогая рабочий инстанс пользователя — сменой home + порта: `THRONE_HOME=$PWD/.throne-agent ./throne -p 5009` (своя база/pid/workspaces; non-TTY ⇒ браузер не откроется), остановка — `THRONE_HOME=$PWD/.throne-agent ./throne stop`. Полная поверхность CLI и модель инстансов — [readme → Запустить](readme.md#2-запустить) и [ADR-0049](specs/ADR/0049-cli-daemon-and-home-instances.md).

## Frontend / UI

Перед любой разработкой `apps/web` или UI-компонентов прочитай [DESIGN.md](DESIGN.md) и используй его как проектную дизайн-систему.

## Что не делать

- Не дублируй сюда содержимое ADR / readme — только ссылки.
- Не описывай тут архитектуру или модели — это ADR.
- Не пиши «недавние изменения» — это `git log`.
- Продуктовая постановка живёт вне репозитория и приходит с запросом пользователя. Не строй предположений из остатков прошлых итераций в коде.
