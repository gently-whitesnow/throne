# ADR-0024: Intent ↔ Repository binding и git-provider shell-out

## Status

Accepted
Date: 2026-05-23
Related: [ADR-0006](0006-openapi-contract-first-codegen.md), [ADR-0008](0008-realtime-contract-first-events.md), [ADR-0014](0014-mcp-initialize-instructions-routing.md)

## Context

Throne расширяется до «единого окна цикла разработки»: к интенту привязывается один или несколько GitHub-репозиториев, под каждый bind создаётся локальный workspace, и в карточке интента отображается PR с лентой review-комментариев. Slice 1 родительского интента
`0fad9876661c450ebf89b86b9335516c` фиксирует функциональный объём; этот ADR закрепляет архитектурные решения, без которых остальные 20 задач slice 1 (T-02..T-21) не имеют точки опоры.

Ключевые открытые вопросы, на которые отвечает этот ADR:

1. Где лежит локальный клон репозитория, как разруливаются коллизии имён и как агент быстро перечисляет все репозитории интента.
2. Как Throne обращается к GitHub/GitLab: пишем свой OAuth-клиент или shell-out в локально установленные CLI.
3. Как доставляются обновления PR-комментариев в local-only-среде без публичных webhook-эндпоинтов.
4. Какие состояния проходит клон и что Throne делает при потере upstream-репозитория.
5. Как себя ведёт ручной refresh PR-комментариев и сосуществует ли он с polling-фанаутом.
6. Какой surface получает MCP в slice 1 — read-only или write.

Альтернативы по каждому вопросу подробно разобраны в секции «Alternatives».

## Decision

### 1. Workspace layout — плоский namespace на интент

Каждый bind клонируется в

```
{Throne:Workspace:Root}/intents/{intent_id}/{owner}__{repo}/
```

- `Throne:Workspace:Root` — конфиг-ключ, default `~/.throne/workspaces`. Создаётся `WorkspaceRootInitializer` (T-05) как `IHostedService` при старте API.
- Разделитель `__` (двойное подчёркивание) между `owner` и `repo` конструктивно исключает коллизии `foo/myrepo` vs `bar/myrepo` без вложенных каталогов. GitHub `owner` и `repo` не содержат `__` (допустимы только `A-Za-z0-9._-`), поэтому split по `__` однозначен.
- Один сегмент на bind означает, что `ls {Throne:Workspace:Root}/intents/{intent_id}/` за один шаг перечисляет все репозитории интента. Это важно для агентов, которые ищут сервисы по корню воркспейса (терминал slice 4, Plannotator slice 2): им не нужен round-trip в Mongo, чтобы понять, какие пути в файловой системе принадлежат интенту.
- При unbind каталог НЕ удаляется (slice 6 — отдельный интент). Bind ↔ workspace_path хранится в Mongo и при ребинде того же `(owner, repo)` в тот же интент даёт 409 (см. T-08).

### 2. Один репозиторий в N интентах — независимые клоны

`{root}/intents/{id1}/owner__repo/` и `{root}/intents/{id2}/owner__repo/` живут параллельно как два независимых git-клона. Изоляция бранчей — намеренный эффект: разные интенты могут работать с разными ветками без конфликтов. Экономия диска через shared bare-repo + worktree-per-intent — out of scope slice 1; рассматриваем отдельным интентом, если упрёмся в диск.

### 3. Порт `IGitProvider` + shell-out в CLI вендоров

Throne НЕ пишет свой OAuth-клиент к GitHub/GitLab. Вместо этого вводится порт `IGitProvider` (Application layer) с операциями: search/list репозиториев, clone, fetch, get PR, list PR-comments (с ETag), auth status. Реализации:

- `GitHubCliProvider` — shell-out в локально установленный `gh` (T-06/T-07).
- `GitLabCliProvider` — shell-out в `glab`, slice 5.

Wrapper `ProcessRunner` (T-05) — единая точка запуска внешних процессов (таймауты, capture stdout/stderr, exit-code, отмена). Никаких прямых `Process.Start` в провайдерах.

Обоснование выбора:

- `gh` / `glab` уже решают OAuth, device-code, refresh-токены, scopes, conditional GET и rate-limit. Дублировать это бессмысленно.
- Юзер ставит `gh` штатным путём (`brew install gh` + `gh auth login`) — Throne не лезет в чужой OAuth-app registration и не хранит долгоживущих токенов.
- Throne — local-only, машина юзера уже доверенная среда; shell-out не вводит новый класс угроз.

### 4. Polling-only доставка PR-комментариев

Webhook'ов нет — Throne работает локально и не имеет публичного endpoint'а, на который GitHub мог бы постучаться. Background-сервис `PullRequestSyncService` (T-10) раз в `Throne:Pr:PollIntervalSeconds` (default 60) ходит по всем активным binding'ам с `pull_request_state == open` через `gh api -H "If-None-Match: {etag}"` и фанаутит новые комменты как `intent.pr_comment_added` (ADR-0008).

- Closed/merged PR не поллятся вообще (экономия rate-limit), но manual refresh всё равно доступен.
- Сохраняем `review_comments_etag` per binding для conditional GET — 304 не тратит rate-limit.
- Backoff на rate-limit / network ошибки.

### 5. Статус-машина `clone_status`

Закрытое множество значений `IntentRepositoryBinding.clone_status`:

```
pending  → cloning  → ready
                  ↘ failed
ready    → broken                  (404 upstream при polling)
```

- `pending` — bind создан, в очереди `RepositoryCloneService` (T-09).
- `cloning` — `gh repo clone` запущен.
- `ready` — клон успешен, workspace_path существует, дальнейшие операции (fetch, PR sync) разрешены.
- `failed` — клон не удался; `clone_error` содержит человекочитаемое сообщение. Юзер делает unbind + rebind.
- `broken` — клон был ready, но upstream больше не отвечает: 404 на `gh api repos/{owner}/{repo}` или на PR. Не пытаемся авто-определить, что произошло (rename/transfer/delete/приват) — это требует guesswork и догадок про политику доступа. Юзер видит статус, решает вручную.

На рестарте API все `cloning`-binding'и переводятся в `failed("interrupted")` (T-09). Авто-retry в slice 1 нет.

Контракт T-02 (OpenAPI + realtime yaml) обязан включать все 5 значений.

### 6. Sync-семантика manual refresh — синхронный + realtime-фанаут

`POST /api/v1/intents/{intent_id}/repositories/{binding_id}/sync`:

- Эндпоинт блокирует запрос на время `gh api`-вызова и возвращает свежие комменты прямо в ответе. Это естественнее для UI-инициированного действия — кнопка «обновить» не должна оставлять пользователя гадать про асинхронный фанаут.
- Параллельно `intent.pr_comment_added` всё равно фанаутится — другие открытые вкладки/клиенты получают тот же дифф через стандартный realtime pipeline (ADR-0008).
- Manual sync работает на closed/merged PR тоже: `pull_request_state` игнорируется. 404 upstream → `binding.MarkBroken`.

### 7. 404 на upstream → `broken`, без авто-предложений re-bind

Когда polling или manual refresh ловит 404 на `gh api repos/{owner}/{repo}` или PR-эндпоинте, binding переходит в `broken` с сообщением. UI показывает статус, но не предлагает автоматически переcоздать bind:

- Причин у 404 много (rename, transfer, удаление, потеря доступа, временная недоступность), а GitHub API не возвращает причину консистентно.
- Авто-rebind по новой паре `(owner, repo)` требует guesswork и может поломать workspace_path / историю.

Юзер решает вручную: unbind + новый bind. Если станет регулярной болью — расширяем отдельным интентом.

### 8. MCP — read-only by design в slice 1

`bind_repository` / `unbind_repository` / `sync_repository` через MCP осознанно НЕ даём. Write-операции — только UI, потому что:

- Bind репозитория к интенту — продуктовое решение пользователя (выбор `(owner, repo)`, PR-number, default branch), а не «работа агента над интентом».
- MCP write-surface множит способы случайно сломать workspace и расходится с принципом slice 1 («первый usable срез, не оверинженерим»).

В slice 1 MCP получает read-only расширение `get_intent.repositories[]` (T-13) и `list_intent_pr_comments` — этого хватает, чтобы агент видел контекст репозитория и читал PR-фидбек. Расширение write-surface (если потребуется) — отдельный интент по запросу.

## Alternatives

### Workspace layout

- **`{root}/intents/{intent_id}/{repo}/`** — простой плоский namespace, но коллизионен: `foo/myrepo` и `bar/myrepo` бьются в одну директорию. Решать конфликт суффиксами (`myrepo_2`) — путь к багам синхронизации path ↔ Mongo. Отклонено.
- **`{root}/intents/{intent_id}/{owner}/{repo}/`** — устраняет коллизии, но требует два `ls` (или `find -maxdepth 2`), чтобы агент перечислил все репы интента. Также требует логики разрешения пути «у меня workspace_path, какому intent_id он принадлежит» через парсинг сегментов или Mongo-lookup. Отклонено в пользу плоского `owner__repo`.
- **Глобальный pool `{root}/repos/{owner}__{repo}/`** + symlink-фермы под интенты — экономит диск (один клон на репу), но ломает изоляцию бранчей (см. решение 2) и плохо переживает unbind. Отклонено как преждевременная оптимизация.

### Git-provider

- **Свой HTTP-клиент к GitHub REST/GraphQL поверх Octokit.NET** — даёт строгую типизацию, но требует встроенного OAuth-app registration, refresh-токенов, scopes-менеджмента и хранения секретов. Throne — local-only single-user, дублировать `gh` бессмысленно. Отклонено.
- **`libgit2sharp` для clone/fetch + HTTP для GitHub-API** — два разных канала аутентификации (git credentials vs API token), две точки отказа. Отклонено.

### PR-comment delivery

- **Webhook'и от GitHub** — требуют публичного endpoint'а / туннеля (ngrok/cloudflared) на машине юзера. Это меняет deployment-модель Throne с local-only на «нужен tunnel»; противоречит позиционированию slice 1. Отклонено.
- **SSE/long-poll от GitHub** — GitHub такого API не предоставляет; для GraphQL Subscriptions требуется enterprise plan. Отклонено.
- **Polling раз в N секунд через `gh api`** — выбран (см. Decision 4). Conditional GET через ETag минимизирует cost.

### Manual sync semantics

- **Async (202 + событие)** — единообразно с clone-pipeline, но UI-кнопка «обновить» в этом случае требует отдельной обработки «когда же придёт событие». Для одной кнопки overhead больше, чем выгода. Отклонено в пользу синхронного ответа + параллельный realtime (Decision 6).

### Reaction на 404 upstream

- **Авто-предложение re-bind на основе redirect'ов GitHub** — GitHub API не всегда возвращает старый URL/новый owner; для transfer'ов это работает, для rename — частично, для delete — никогда. Слишком много edge cases для slice 1. Отклонено.

### MCP surface

- **Full write-surface (bind/unbind/sync через MCP)** — увеличивает поверхность атаки workflow-ошибок (агент случайно делает unbind), а продуктового спроса в slice 1 нет: пользователь сам выбирает репу в UI. Откладываем до явного запроса.

## Out of scope

- Embedded терминал в карточке интента — slice 4.
- Plannotator-интеграция — slice 2.
- Диаграммы / артефакты в UI — slice 3.
- GitLab provider — slice 5.
- Авто/manual удаление workspace при unbind — slice 6.
- Авто-rebind на rename/transfer репозитория — отдельный интент после slice 1.
- Shared bare-repo + worktree-per-intent для экономии диска — отдельный интент, если упрёмся.
- Per-intent размер workspace — slice 1 показывает только глобальный (см. T-17).
- Write-операции `bind_repository` / `unbind_repository` / `sync_repository` через MCP — отдельный интент по запросу.

## Consequences

### Positive

- Все 20 задач slice 1 (T-02..T-21) ссылаются на фиксированный layout и контракт `IGitProvider` — нет повторных «давайте обсудим, где клон лежит».
- Workspace layout `{intent_id}/{owner}__{repo}/` даёт O(1) перечисление всех реп интента через `ls` — критично для будущих агент-инструментов (терминал, Plannotator).
- Shell-out в `gh` снимает с Throne весь OAuth-стек и аудит токенов; secret-management — забота вендорного CLI.
- Polling с conditional GET через ETag вписывается в free-tier rate-limit (`gh api` использует тот же `Authorization: Bearer`, что и `gh auth login`), и не требует webhook-инфраструктуры.
- Статус `broken` отделён от `failed`: UI может показать разные сообщения, а сервис может игнорировать `broken` в фоне без потери истории.
- Read-only MCP-surface в slice 1 минимизирует риск ошибок агентов на ранней стадии — write добавим, когда появится явный продуктовый сценарий.

### Negative / Risks

- **Зависимость от установленного `gh`** на машине пользователя — добавляем precondition к демо («`brew install gh` + `gh auth login`»). Mitigation: settings-страница (T-16) показывает явный red/green индикатор и ссылку «как настроить gh».
- **ProcessRunner shell-out** труднее тестировать, чем in-process HTTP-клиент: интеграционные тесты `GitHubCliProvider` (T-06/T-07) требуют либо реальный `gh` в CI, либо стабы. Mitigation: основные тесты — на стабе через `IProcessRunner`, e2e — отдельная категория, опциональная.
- **Polling с интервалом 60s** — комменты в PR появляются с задержкой до минуты. Для одного-двух пользователей это допустимо; интервал — конфиг, при необходимости снижается.
- **Каталоги `broken`-binding'ов остаются на диске** — slice 6 разруливает; до тех пор пользователь видит mёртвые workspace_path. Acceptable для MVP.
- **MCP не умеет bind/unbind** — если агент по сценарию «возьми PR, проанализируй» хочет сам привязать репу, он этого не сделает; должен попросить юзера. Acceptable: явное действие пользователя — не баг, а фича slice 1.
