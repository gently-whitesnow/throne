# ADR-0032: GitLab как второй git-провайдер

## Status

Accepted
Date: 2026-06-07
Related: [ADR-0024](0024-intent-repository-binding-and-cli-providers.md), [ADR-0030](0030-mcp-surface-policy-cli-first.md), [ADR-0026](0026-embedded-terminal-capabilities-and-run-preflight.md), [ADR-0006](0006-openapi-contract-first-codegen.md), [ADR-0008](0008-realtime-contract-first-events.md)

## Context

[ADR-0024](0024-intent-repository-binding-and-cli-providers.md) спроектировал привязку репозиториев к интентам provider-neutral (порт `IGitProvider` + `IGitProviderRegistry`, shell-out в вендорный CLI через `IProcessLauncher`), но реализовал только GitHub-провайдер поверх `gh`, явно вынеся GitLab в «отдельный интент». Этот ADR закрывает GitLab наравне с GitHub: оператор хочет вести рабочие задачи в **self-managed (корпоративном)** GitLab так же, как сейчас в GitHub — привязка репозитория, MR в карточке интента, поллинг комментариев, авто-привязка по ветке и авто-закрытие по мерджу.

Архитектура уже provider-agnostic by construction: резолв провайдера идёт через `providers.GetByName(binding.Coordinate.Provider)`, путь к CLI параметризован, состояние PR сравнивается строками `open`/`closed`/`merged`, дельта новых комментариев считается watermark'ом `LastSeenReviewCommentAt`, а сетевые сбои уводят binding в тихий backoff без потери состояния. Поэтому решения ниже — это точечная достройка под `glab` и обобщение мест, где GitHub оказался зашит, а не новый слой.

Открытые вопросы, на которые отвечает ADR: как кодировать вложенные неймспейсы GitLab в координате и workspace-layout; где живёт адрес self-managed инстанса; как поллить комментарии MR без ETag; как замапить операции `gh` → `glab`; как сделать scope поиска репозиториев общим для обоих провайдеров; как относиться к временной сетевой недоступности корпоративного хоста.

## Decision

### 1. `GitLabCliProvider` поверх `glab`, без своего OAuth

Новый `GitLabCliProvider : IGitProvider` shell-out в локально установленный `glab`, регистрируется в `IGitProviderRegistry` рядом с `GitHubCliProvider`; `GitLabCliOptions.ExecutablePath` (default `glab`). Своего OAuth-клиента не пишем (принцип ADR-0024): аутентификацию ставит пользователь (`glab auth login --hostname <host>`).

Read-операции реализуем через сырой `glab api` (не типизированные команды): `glab mr note list` помечен experimental, `glab repo list` не отдаёт нужные фильтры, а `glab api` стабилен между версиями и, как `gh api -i`, умеет `-H` (заголовки запроса) и `-i` (заголовки ответа). Маппинг операций `IGitProvider`:

| Операция | Команда |
|---|---|
| auth status | `glab api user --hostname <host> -i` (у `glab auth status` нет JSON) |
| clone | `glab repo clone <path> <dir> -- --filter=blob:none` (хост через `GITLAB_HOST`) |
| list branches | `glab api projects/:id/repository/branches` |
| list MR | `glab api "projects/:id/merge_requests?state=opened"` |
| get MR | `glab api projects/:id/merge_requests/:iid` |
| MR comments | `glab api projects/:id/merge_requests/:iid/discussions --paginate` |
| version preflight | `glab version` |

`:id` в таблице — это url-энкоженный `path_with_namespace` проекта (см. Decision 2), а не числовой `project_id`. Список MR тянется один раз со `state=opened` и фильтруется на стороне Throne (по `source_branch` для авто-привязки в Decision 5, по подстроке для typeahead) — паритет с GitHub-провайдером, который тоже листит открытые PR и матчит head-ref в C#; узкого server-side `source_branch`-запроса порт `IGitProvider` не выражает (метод листинга принимает только подстроку-`query`).

### 2. Координата и workspace-layout для вложенных неймспейсов

GitLab-проект живёт как `path_with_namespace` = `group/subgroup/project` (вложенность до 20 уровней). `RepoCoordinate` обобщается (не вводим отдельный `GitLabRepoCoordinate`): для GitLab `Owner` = namespace-path (`group/subgroup`), `Repo` = leaf-проект. Валидация становится provider-aware — GitHub остаётся строгим (`^[A-Za-z0-9][A-Za-z0-9-]{0,38}$`), GitLab валидирует каждый `/`-сегмент по slug-правилам (начинается/кончается `[A-Za-z0-9]`, внутри `_ . -`).

`WorkspacePathLayout` строит каталог `{owner}__{repo}` и **никогда не парсит его обратно** (координата всегда читается из Mongo), поэтому имя каталога может быть любым FS-safe: `/` в namespace заменяем на `-`, плоский `ls` и O(1)-перечисление реп интента (ADR-0024 § 1) сохраняются. Защитный `__`-guard для GitLab-owner ослабляется (обратный split не используется).

REST-адресация GitLab идёт по url-энкоженному `path_with_namespace` (`Uri.EscapeDataString("group/subgroup/project")`), который `glab api` принимает наравне с числовым id. Числовой `project_id` дополнительно сохраняем на binding (он приходит «бесплатно» в ответе поиска проектов), но как **forward-looking/reserved** поле, а не как ключ адресации: порт `IGitProvider` намеренно адресуется парой `(owner, repo)` и не несёт ни `project_id`, ни `host` (host тоже резолвится вне порта — из глобальной настройки, Decision 3). Протаскивать GitLab-специфичный `project_id` скаляром в provider-neutral порт ради устойчивости к rename сейчас непропорционально: rename группы/проекта у уже привязанного репо — редкий кейс, дешёвое восстановление — re-bind, а clone/sync идут через локальный git-remote и rename API-пути не замечают. Если устойчивость к rename станет требованием (или появится per-binding multi-host), порт меняется на передачу объекта-координаты (`RepoCoordinate`/handle), где `host` и `project_id` едут связно, — отдельным решением, без скалярного параметра.

### 3. Self-managed host

`RepoCoordinate`/binding получают поле `Host` (для GitHub неявно `github.com`, для GitLab обязателен), персистится в Mongo. `GitLabCliProvider` прокидывает `GITLAB_HOST` в env на каждый вызов `glab` (`ProcessRunRequest.Environment` уже поддерживается) — не полагаемся на детект по git-remote.

MVP — один глобальный GitLab-host в настройках (`Throne:GitLab:Host` + поле в Settings-контракте рядом с зарезервированным `gitlab` в `GitProvidersStatusDto`), но `Host` хранится на binding, чтобы per-binding multi-host добавился позже без миграции. `gitlab`-entry в `GitProvidersStatusDto` заполняется probe'ом `glab api user --hostname <host> -i` (симметрично `gh`).

TLS: полагаемся на системный trust store — corp-CA пользователь ставит в ОС; Throne TLS не конфигурирует и проверку не отключает. Транспорт clone/fetch — на готовых SSH/HTTPS-кредах пользователя (SSH в приоритете); git-креды Throne не трогает.

### 4. Поллинг комментариев MR без ETag

Документированного ETag/304 в GitLab REST нет. Это не проблема: дельта новых комментариев уже считается watermark'ом `LastSeenReviewCommentAt` (`PullRequestCommentCursor.FilterNew` по `CreatedAt`), а ETag в GitHub-флоу — лишь оптимизация. `GitLabCliProvider` не шлёт `If-None-Match` и всегда возвращает `PullRequestCommentsPage.Fresh(comments, etag: null)`; поле `ReviewCommentsEtag` для GitLab просто не используется — **схема и контракт не меняются**.

Комментарии берём из **Discussions API** (threaded, привязка к диффу через `position{base/head/start_sha, old/new_path, old/new_line}`) — это GitLab-аналог GitHub review-comments; системные notes фильтруем. Дешёвая оптимизация (опционально): тянуть discussions только когда MR `updated_at` сдвинулся (MR-снапшот и так читается каждый тик).

### 5. Авто-привязка по ветке и авто-закрытие по мерджу

Флоу ADR-0024 § 9 provider-agnostic и не меняется. `GitLabCliProvider.GetPullRequestAsync` нормализует состояние MR в существующее множество `PullRequestStateNames`: `opened → open`, `merged → merged`, `closed → closed`, `locked → open`. Авто-привязка переиспользует общий `PullRequestAutoBindWorkflow`: head-ветка локального клона (`git -C <ws> rev-parse --abbrev-ref HEAD`) сопоставляется с `HeadRef` открытых MR из `ListPullRequestsAsync` (матч на стороне Throne, как у GitHub-провайдера); ровно одно совпадение → `AttachPullRequest`. Серверный `source_branch`-фильтр не используется — порт листинга узкого запроса не выражает (Decision 1).

### 6. Общий scope поиска репозиториев для обоих провайдеров

`RepositorySearchScope { Mine, Involved }` уже реализован для GitHub на бэке (`Involved` = свои + collaborator/org-member), но в UI автокомплита не выведена галочка. Выводим её для обоих провайдеров. GitLab маппинг: `Mine → GET /projects?owned=true`, `Involved → GET /projects?membership=true` (все проекты-членства по инстансу). «Всё доступное пространство» трактуем как membership-of (паритет с GitHub, не вываливаем тысячи public-проектов); инстанс-wide текстовый поиск (`/search?scope=projects`) — за рамками этого ADR.

### 7. Временная сетевая недоступность корпоративного хоста — норма, не ошибка

Корпоративный GitLab периодически недоступен (вне VPN/контура). Это ожидаемое транзиентное состояние: `GitProviderErrorKind.NetworkError` уже уводит binding в тихий экспоненциальный backoff (`PullRequestSyncBackoff`, 30s→max 900s), binding остаётся `ready` (в `broken` уводит только 404), на возврате сети тик ретраит автоматически. ADR закрепляет три достройки, чтобы недоступность не выглядела как сбой:

- **Не спамить логом.** Для `NetworkError` (ожидаемое состояние) уровень логирования понижается с `Warning` до `Debug`/`Information` и дедуплицируется (переход в «недоступен» + периодический summary, не каждый тик). `AuthFailed`/`CliFailure` остаются `Warning`.
- **`GlabErrorClassifier`.** Аналог `GhErrorClassifier`, распознающий сетевые паттерны `glab` (refused/unreachable/timeout/TLS/proxy/5xx) → `NetworkError`, иначе мягкий backoff не сработает.
- **UI «вне сети», а не красная ошибка.** Auth-probe вне сети возвращает `NetworkError` → индикатор провайдера показывает янтарное «недоступен / вне сети» (транзиентно), отдельно от красного «не залогинен» (`AuthFailed`); binding в backoff на карточке — «ждём доступа», не «ошибка».

Инвариант: сетевая недоступность никогда не ведёт к `broken`/`failed` и не теряет состояние.

### 8. Capability-гейтинг и обобщение эндпоинтов

`gitlab` (уже в `CapabilityNames`) = «разрешить второй провайдер», default OFF; `repositories` (ADR-0026) = фича привязки реп. GitLab-флоу требует обе ON. Onboarding в настройках: поставить `glab`, `glab auth login --hostname <host>`, ввести host; red/green/янтарь-индикатор через `glab version` + auth-probe.

Захардкоженные под GitHub эндпоинты (`ListMyGithubRepositoriesEndpoint`, `SearchGithubRepositoriesEndpoint`, `ListGithubRepositoryBranchesEndpoint`, `ListGithubRepositoryPullRequestsEndpoint`) обобщаются на route `/api/v1/git-providers/{provider}/repositories/...` с одним хендлером, резолвящим провайдер через registry; классы переименовываются в provider-neutral. OpenAPI обновляется → регенерация DTO/типов (NSwag/openapi-typescript), gate `contracts` ловит drift. Throne pre-1.0/local — back-compat старого `/github/`-пути не держим.

MCP-surface остаётся read-only (ADR-0024 § 8, ADR-0030): MR-комментарии агент читает CLI-провайдером `glab` прямо в workspace, отдельного MCP-tool не вводим.

### 9. MR ↔ PR — переиспользуем абстракцию, не переименовываем

Merge Request маппится на существующую provider-neutral «PullRequest»-абстракцию: модель, OpenAPI/realtime-контракты (`intent.pr_comment_added` и пр.) и MCP `get_intent.repositories[]` не переименовываем. `glab` приводит MR → ту же модель; в UI-видимых местах GitLab можно косметически показывать «MR».

## Alternatives

- **Отдельный `GitLabRepoCoordinate`** вместо обобщения `RepoCoordinate` — больше дубля в домене и в местах резолва; отклонено в пользу provider-aware валидации единого типа.
- **Переименование PR → ChangeRequest/MergeRequest** — семантически чище, но ломает OpenAPI/realtime-контракты, UI и MCP ради косметики; отклонено (Decision 9).
- **Эмуляция ETag/304 для GitLab** (хранить хэш ответа, сравнивать) — лишняя машинерия: watermark по `created_at` уже даёт корректную дельту; отклонено (Decision 4).
- **Кастомный CA-bundle в настройках / `GIT_SSL_NO_VERIFY`** — расширяет поверхность и/или небезопасно; системный trust store закрывает кейс (Decision 3).
- **Multi-host per-binding сразу** — преждевременно для одного корпоративного инстанса; `Host` на binding оставляет путь к этому без миграции (Decision 3).
- **Инстанс-wide `/search?scope=projects` как дефолтный scope** — вываливает тысячи public-проектов, расходится с GitHub-паритетом; отклонено (Decision 6).

## Out of scope

- Реализация (этот ADR — постановка; работа идёт отдельными слайсами-интентами S0–S3, см. ниже).
- Multi-host per-binding и UI выбора из нескольких GitLab-инстансов.
- Инстанс-wide текстовый поиск проектов (`/search?scope=projects`).
- Создание MR/комментариев из Throne (read-флоу; write — по отдельному запросу).
- Bitbucket и прочие провайдеры.
- Realtime-событие смены «доступен ↔ недоступен» (сейчас событий на sync-fail нет; не вводим без нужды).

## Consequences

### Positive

- GitLab встаёт в существующий контракт `IGitProvider` без нового слоя; поллинг, backoff, авто-привязка и авто-закрытие переиспользуются as-is.
- Provider-aware валидация и `Host` на binding оставляют дверь к multi-host и другим провайдерам открытой без миграций.
- Сетевая недоступность корпоративного хоста обрабатывается как штатное состояние — без лог-шума и ложных «ошибок», с авто-возобновлением.
- Обобщение repo-эндпоинтов на `{provider}` убирает GitHub-хардкод и распространяется на будущих провайдеров.

### Negative / Risks

- **Зависимость от `glab`** на машине пользователя — новый precondition; mitigation: индикатор + onboarding в настройках (симметрично `gh`).
- **Нет ETag → каждый тик тянет discussions целиком** для открытых MR; на self-managed rate-limit'ы обычно отключены/щедрые, поллинг раз в 60s имеет запас; опц. оптимизация по MR `updated_at` (Decision 4).
- **Provider-aware валидация координаты** — точка, где легко внести расхождение GitHub/GitLab; покрывается юнит-тестами на slug-правила и на `owner__repo`-layout.
- **TLS вне зоны Throne** — если corp-CA не стоит в системе, clone/api упрутся в проверку сертификата; это сознательно ответственность пользователя/ОС (Decision 3).
- **Обобщение эндпоинтов ломает старый `/github/`-путь** — допустимо для pre-1.0 local-only; фронтенд переключается синхронно через регенерацию типов.
