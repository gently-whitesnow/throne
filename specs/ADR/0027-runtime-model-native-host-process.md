# ADR-0027: Runtime model — нативный хост-процесс для host-фич

## Status

Accepted
Date: 2026-06-04
Related: [ADR-0024](0024-intent-repository-binding-and-cli-providers.md), [ADR-0026](0026-embedded-terminal-capabilities-and-run-preflight.md), [ADR-0029](0029-local-first-invariant-and-legacy-auth.md) (local-first, `Auth:Mode=Disabled` дефолт; внутренняя авторизация — легаси)

## Context

Throne превращается из «слоя памяти/постановки, к которому подключаются агенты» в **кокпит цикла разработки** (единое окно: intent → агент → PR → ревью → доработка). Это прямое следствие родительского эпика «единое окно цикла разработки».

Текущая упаковка (docker-compose, API в контейнере) структурно конфликтует с кокпитом: встроенный терминал, кнопка Run, кнопки VS Code и `gh`/`glab`-shell-out (ADR-0024, ADR-0026) требуют доступа к **хосту**, которого у контейнера нет. Чтобы накормить контейнер хостовым состоянием, в `docker-compose.yml` и readme «Запуск» накопились хаки:

- выгрузка токенов `gh`/`claude` из Keychain в plaintext (`gh auth login --insecure-storage`, `security find-generic-password > ~/.claude/.credentials.json`), чтобы bind-mount пробросил их в контейнер;
- проброс `~/.ssh` read-only без форвардинга `ssh-agent` → запароленные ключи ломаются;
- `vscode`-capability навсегда `detected=false`: `code` — хостовый GUI-бинарь, в контейнере его нет.

Корень боли — ось **рантайм** (откуда живёт backend), а не ось **CLI-proxy**. Решение «shell-out в готовые `gh`/`glab`/`claude` вместо своих OAuth-клиентов» (ADR-0024) остаётся правильным и не меняется. Меняем только то, откуда запускается API.

## Decision

Throne получает **два режима запуска**; host-фичи становятся явным opt-in по рантайму.

### 1. Контейнерный режим (дефолт)

`docker-compose.yml --profile full` — весь стек (api + web + mongo) в контейнере, как раньше. Host-capabilities (`repositories` / `terminal` / `vscode`) в нём **OFF** через штатный capability-механизм (ADR-0026 § 1, default `enabled=false`, detection не флипает тогл). Хаки удалены: из `api`-сервиса убраны bind-mount'ы `~/.config/gh`, `~/.ssh`, `~/.claude`, `~/.claude.json`, а также `Throne:Workspace:HostRoot` и mount `~/.throne/workspaces` — они нужны были только чтобы накормить контейнер host-фичами, которых в этом режиме нет. Контейнерный `api` остаётся чистым: Mongo + ASP.NET.

### 2. Host-backend режим (продвинутые)

Отдельный трекаемый `docker-compose.host.yml` поднимает **только web + mongo** (+mongo-init), без `api`. API запускается нативно на хосте `dotnet run` и наследует хостовый PATH → спавнит `code`/`claude`/`gh`/`git`/`tmux` напрямую, ходит в OS keychain через сами CLI (без plaintext-экспорта), использует реальный `ssh-agent`, открывает хостовые GUI. На хосте capability-detection видит реальные тулы: `vscode` `detected=true` когда `code` в PATH; `terminal`/`repositories` пробятся против хостовых `tmux`/`gh`.

Конфиг host-режима — дефолты, ничего специального:

- `Auth:Mode=Disabled` — local-first (это и так дефолт `AuthOptions.Mode`).
- `Mongo:ConnectionString` → `localhost:27017` (контейнерная Mongo проброшена портом; и так дефолт `MongoOptions`).
- `Throne:Workspace:Root` → `~/.throne/workspaces` — **реальный хостовый путь без HostRoot-трансляции** (`HostRoot` — контейнерный артефакт, нужный только чтобы показать оператору путь на хосте; нативно Root уже и есть хостовый путь).
- `ASPNETCORE_URLS=http://0.0.0.0:5008` — обязательно: web-контейнер ходит в API через `host.docker.internal`, а это не loopback. Биндинг на `0.0.0.0` приемлем для local-trust аудитории host-режима (Auth Disabled, машина оператора).

Mongo и API — две независимые команды, без оркестрации между ними (контейнер web+db отдельно, host-API отдельно).

### 3. Доставка host-API контейнерному web + CORS

Web остаётся в контейнере и указывает на host-API. Web-контейнер уже reverse-проксирует `/api/` через nginx, поэтому **base-URL и CORS не трогаем**: браузер всегда ходит на тот же origin (`web:8080`), а nginx-upstream параметризуется одной env-переменной `THRONE_API_UPSTREAM`:

- контейнерный режим — `api:5008` (дефолт, зашит в образ);
- host-backend — `host.docker.internal:5008` (выставляет `docker-compose.host.yml`; на Linux добавлен `extra_hosts: host.docker.internal:host-gateway`).

Подстановка — штатным envsubst-энтрипоинтом nginx (`templates/ → conf.d/`), `NGINX_ENVSUBST_FILTER=THRONE_` ограничивает её нашим префиксом. Заодно в `/api/`-локацию добавлен WebSocket-upgrade (`Upgrade`/`Connection`) — встроенный терминал (ADR-0026) ходит bidirectional-каналом, без проброса nginx закрыл бы handshake.

### 4. Settings UI

На странице `/settings` в секции «Возможности» добавлена пометка: терминал/Run/vscode/repositories требуют запуска бэкенда на хосте (профиль «только web+db»), иначе фичи не детектятся и остаются выключены.

## Consequences

- Хаки исчезают в обоих режимах: в контейнерном host-фич нет (нечего хакать), в host-режиме нативный API шеллаутит в хостовые `gh`/`claude` с родным keychain/ssh-agent. Plaintext-токенов, проброса `~/.ssh` и «кнопок VS Code нет в контейнере» больше нет.
- Переопределяет **неявную Docker-предпосылку эпика**: backend не обязан жить в контейнере.
- Влияет на [ADR-0026](0026-embedded-terminal-capabilities-and-run-preflight.md): terminal/capabilities больше не container-bound — их «хостовость» теперь буквальная (нативный процесс), а не «контейнер поверх примонтированного хостового состояния».
- [ADR-0024](0024-intent-repository-binding-and-cli-providers.md) (CLI-proxy через `gh`/`glab`) явно **остаётся в силе** — меняется рантайм, а не способ интеграции с git-провайдерами.
- readme «Запуск» переписан под host-флоу; секции-хаки удалены.

## Out of scope

- Tauri/desktop-приложение и упаковка API в global-tool/binary — `dotnet run` на хосте приемлем для продвинутой аудитории; отдельная упаковка — возможный будущий интент.
- Windows-паритет встроенного терминала (`tmux` unix-only) — известный гэп, не решается здесь.
- Замена Mongo на embedded-хранилище.
