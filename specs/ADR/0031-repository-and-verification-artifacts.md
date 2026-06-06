# ADR-0031: Repository-знание и PR-верификация — два сфокусированных контракта

## Status

Accepted
Date: 2026-06-06
Related: [ADR-0024](0024-intent-repository-binding-and-cli-providers.md), [ADR-0025](0025-domain-aggregate-style-rich-ddd.md), [ADR-0026](0026-embedded-terminal-capabilities-and-run-preflight.md), [ADR-0030](0030-mcp-surface-policy-cli-first.md), [ADR-0008](0008-realtime-contract-first-events.md), [ADR-0006](0006-openapi-contract-first-codegen.md), [ADR-0002](0002-domain-model-and-text-versioning.md)

## Context

Эпик «единое окно цикла разработки» порождает производное знание, которому негде жить как durable-данным: карта схемы БД репозитория (Slice 3) и выходы верификации работы агента над PR (статанализ, описание тестов/шагов, покрытие, диаграммы вызовов/классов/взаимодействия, AI-рекомендации по порядку чтения файлов на ревью). Сегодня это либо не существует, либо рискует осесть в `Intent.text` (шумит историю, [ADR-0019](0019-intent-events-unified-history.md)) или в одноразовых полях агрегата.

Соблазн — одна обобщённая «артефактная» сущность с дискриминатором scope. Её отвергаем: у двух классов выходов **разный жизненный цикл, разная аудитория, разная провенанс и разный якорь**. Общая модель даёт «то одни поля null, то другие» — это и есть запах генерик-CMS, от которого предостерегает сам сценарий. Две маленькие сфокусированные сущности честнее, чем одна склейка.

Различие зафиксировано в интервью:

| Ось | Repository-знание | PR-верификация |
|---|---|---|
| Якорь | `Repository` (координата `(provider, owner, repo)`, [ADR-0024](0024-intent-repository-binding-and-cli-providers.md)), видно во всех привязанных интентах | PR конкретного binding'а (`binding_id` + `pull_request_number`), **не intent-scoped** |
| Жизненный цикл | долгоживущее, **версионируется с историей развития** | **one-shot**, latest per `(PR, type)`, перезаписывается при перегенерации |
| Провенанс | AI/human-курируемое, то, что **нельзя статически вычислить** | в основном статически вычислено (диаграммы, покрытие) + часть от AI (рекомендации ревью) |
| Аудитория | приватно владельцу, **не подаётся в контекст агента** | для человеческого ревью PR, **только UI**, не в контекст агента |
| Форма | свободные titled markdown-страницы знаний | типизированный one-shot результат (markdown/mermaid/svg/json) |

## Decision

Заводим **две независимые сущности** (rich-DDD по [ADR-0025](0025-domain-aggregate-style-rich-ddd.md), Throne-owned Mongo), а не одну ось.

### `RepositoryArtifact` — знание о репозитории

Свободная **titled markdown-страница знаний**, прицепленная к `Repository`. `db_schema_map` — не жёсткий тип, а одна из страниц по соглашению (стабильный `slug=db-schema-map` со spec-рендером mermaid erDiagram). Будущее знание (например, «архитектурный обзор», «инварианты домена») — просто новая страница, без правки enum.

Поля:
- `id`, `repository_coordinate` `(provider, owner, repo)`
- `slug` — стабильный идентификатор страницы в пределах репо (уникальность `(coordinate, slug)`)
- `title`, `document` (markdown)
- `render_hint` — `markdown` по умолчанию; `schema_map` включает affordances erDiagram
- `source` — `agent` | `user`
- `version`, `created_at`, `updated_at`

**История развития** — обязательна: append-only снапшот на каждую версию (`repository_artifact_versions`, full-snapshot + author + timestamp; страницы правятся редко, delta-формат [ADR-0002](0002-domain-model-and-text-versioning.md) избыточен). Optimistic concurrency через `expected_version` + typed `ApiException`. UI показывает таймлайн версий.

**В контекст агента не подаётся.** Единственная MCP-точка — узкий authoring-tool, которым агент в сессии-генерации (режим `schema_map`, [ADR-0014](0014-mcp-initialize-instructions-routing.md)) пишет страницу. Обобщаем именованный `write_repository_schema` ([ADR-0030](0030-mcp-surface-policy-cli-first.md)) в страница-ориентированные `write_repository_document(provider, owner, repo, slug, title, document, expected_version?)` + `get_repository_document(...)`, чтобы будущие страницы переиспользовали один narrow write, а не плодили tool-на-страницу. В `get_intent` страницы **не попадают**.

### `PullRequestArtifact` — верификация PR

One-shot результат проверки конкретного PR. Реализует «verification artifacts» эпика, но **PR-scoped, не intent-scoped** (по интенту артефакт смысла не имеет).

Поля:
- `id`, `binding_id`, `pull_request_number`
- `type` — открытая строковая метка (`static_analysis`, `test_summary`, `coverage`, `call_diagram`, `class_diagram`, `review_recommendation`, …); не закрытый enum, потому что состав производителей ещё не определён
- `render` — `markdown` | `mermaid` | `svg` | `json` (как отрисовать)
- `content`, `summary` (короткий заголовок для быстрого ревью)
- `source` — `static` | `agent`
- `source_refs` — провенанс (commit sha, инструмент)
- `produced_at`

**Retention — latest per `(binding_id, type)`**: перегенерация заменяет предыдущий результат этого типа. Истории версий нет (чистый one-shot). Хранится в Mongo, а не в клоне — переживает cleanup workspace ([Slice 6 эпика](88148505)) и привязано к durable binding'у, не к эфемерной рабочей копии.

**Только UI, не в контекст агента и не в MCP.** Producer/ingestion **намеренно оставлен открытым** — состав источников (статические диаграммы без AI, AI-рекомендации ревью, прочая статика) ещё не определён. Фиксируем нейтральный durable write-path: внутренний порт `IPullRequestArtifactSink` за идемпотентным REST-ingest (`PUT …/repositories/{binding_id}/artifacts/{type}`, latest-wins). Любой будущий producer (агент, кладущий файл в `{workspace}/.throne/artifacts/` с последующим ingest; статический CLI, вызванный Throne; локальный скрипт) сходится на этом контракте. Конкретные производители — отдельным слайсом.

### MCP-поверхность ([ADR-0030](0030-mcp-surface-policy-cli-first.md))

Чистый итог — **сужение**, а не расширение:
- `RepositoryArtifact`: только authoring `write_repository_document` / `get_repository_document` (сессия `schema_map`). Не в `get_intent`.
- `PullRequestArtifact`: **MCP не трогает вовсе** (human-only). Никаких `get_intent.artifacts[]`.

### HTTP / контракты ([ADR-0006](0006-openapi-contract-first-codegen.md))

- `RepositoryArtifact`: list/get страниц репо, get/put страницы (`expected_version`), get истории версий.
- `PullRequestArtifact`: list по binding'у, get по `(binding_id, type)`, idempotent put (ingest).

### UI (реестр панелей детали интента, [ADR-0026](0026-embedded-terminal-capabilities-and-run-preflight.md) / commit `cb37e59`)

- `RepositoryArtifact`: страница репозитория — список страниц знаний, inline-mermaid для `db_schema_map`, markdown-редактор, **таймлайн версий**. На странице интента — рендер страниц каждого привязанного репо.
- `PullRequestArtifact`: новая панель в placement `review`, gate `capability: "repositories"`, рядом с секцией PR-комментариев — латест-артефакты PR (диаграммы/покрытие/AI-рекомендации). Добавляется одним дескриптором в реестр без правки shell.

### Realtime ([ADR-0008](0008-realtime-contract-first-events.md))

Две честные семьи событий под две сущности: `repository.document_updated` и `pull_request.artifact_updated`, стандартный domain-event pipeline. Запланированный Slice 3 `repository.schema_updated` поглощается первой.

### Что меняется в эпике / Slice 3

Slice 3 (`e93593d0`) реализует `db_schema_map` как **первую titled-страницу `RepositoryArtifact`** с историей версий, а не как одиночное schema-поле `Repository`; `write_repository_schema` становится `write_repository_document(slug=db-schema-map, …)`. PR-верификация — **отдельная** сущность `PullRequestArtifact`, не на `Repository`; её producer-контракт открыт.

## Consequences

### Positive

- Каждая сущность внутренне когезивна: нет null-полей «то тут, то там». Repository-знание versioned+private; PR-верификация one-shot+UI-only.
- Новая страница знаний = новый `slug` (+ опц. рендер), новый вид PR-проверки = новый `type`-label — без нового домена/коллекции каждый раз. Acceptance «без top-level домена на тип» выполнен по обеим осям.
- MCP сужается: страницы знаний — один narrow authoring-tool, не в контексте; PR-артефакты — вне MCP. Repository-знание не засоряет контекст агента (явное требование владельца).
- Длинные выходы уходят из `Intent.text`; история интента чистая ([ADR-0019](0019-intent-events-unified-history.md)).

### Negative / Risks

- Две сущности вместо одной — чуть больше кода (две коллекции, два HTTP-модуля, две realtime-семьи). Mitigation: каждая мала и сфокусирована; общий «универсальный» агрегат обошёлся бы дороже на сопровождении (null-поля, ветвление по scope).
- `PullRequestArtifact.type` и producer открыты — риск, что без дисциплины наплодят разнородных меток. Mitigation: latest-per-`(PR,type)` и REST-ingest фиксируют форму; конкретные типы/производители вводятся слайсом с обоснованием, а не явочным порядком.
- `document`-as-markdown — слабая типизация payload'а (нет схемной гарантии валидного mermaid). Mitigation: валидация per-`slug`/`type` на write-границе там, где даёт ценность; не выносим в агрегат.
- Триггер на пересмотр: если PR-верификация обрастёт версионной историей или попадёт в контекст агента — это сигнал, что граница сместилась; тогда отдельным ADR, а не разворотом обеих сущностей в общую склейку.
