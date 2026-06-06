# ADR-0031: Repository- и verification-артефакты — единая типизированная ось

## Status

Accepted
Date: 2026-06-06
Related: [ADR-0024](0024-intent-repository-binding-and-cli-providers.md), [ADR-0025](0025-domain-aggregate-style-rich-ddd.md), [ADR-0026](0026-embedded-terminal-capabilities-and-run-preflight.md), [ADR-0030](0030-mcp-surface-policy-cli-first.md), [ADR-0008](0008-realtime-contract-first-events.md), [ADR-0006](0006-openapi-contract-first-codegen.md)

## Context

Эпик «единое окно цикла разработки» порождает несколько видов производного знания, которое сейчас негде хранить как durable-данные: карта схемы БД (Slice 3), сводка верификации работы агента (что изменилось, что рискованно, что требует ещё одного прохода), diff-сводка, выжимка статанализа, архитектурная диаграмма, выжимка истории изменений. Сегодня всё это либо вообще не существует, либо рискует осесть в `Intent.text` (шумит историю, [ADR-0019](0019-intent-events-unified-history.md)) или в одноразовых полях агрегата.

Slice 3 эпика спроектирован так, что `db_schema_map` встраивается прямо в новый агрегат `Repository`. Если зафиксировать эту форму до реализации, каждый следующий вид артефакта (diff/static-analysis/diagram/history) станет либо новым полем на `Repository`/`Intent`, либо новым top-level доменом со своей коллекцией, HTTP-модулем, realtime-семьёй событий и инвалидацией. Это ровно тот «остров на каждую фичу», который множит поверхность.

Два наблюдения формируют решение:

1. Все перечисленные выходы — это **один и тот же вид данных**: типизированный документ (на практике — markdown, часто с inline-mermaid) + лёгкая провенанс-метадата (откуда сформирован, кем, когда). Различается только `type` и **scope** (привязан к репозиторию-в-целом или к работе конкретного интента).
2. Граница «repo-scoped vs intent-scoped» совпадает с уже существующей в эпике границей: `Repository` — единая на систему метадата-сущность (ADR-0024, координата `(provider, owner, repo)`), а верификация — про конкретный проход интента над конкретным PR/binding.

## Decision

Вводим **одну типизированную ось артефактов** вместо отдельного домена под каждый вид. Это нормативное направление для Slice 3 (карта схемы БД реализуется как первый тип на этой оси, а не как поле `Repository`) и для будущих verification-фич.

### Модель

First-class агрегат `Artifact` (rich-DDD по [ADR-0025](0025-domain-aggregate-style-rich-ddd.md)), Throne-owned, одна Mongo-коллекция `artifacts`. Никаких новых сущностей под отдельные `type`. «RepositoryArtifact» и «VerificationArtifact» — это **не два домена, а два scope одного агрегата**, различаемые дискриминатором `scope`:

- `scope = repository` — знание о репозитории-в-целом, видимое во всех интентах, где репо привязан (первый тип — `db_schema_map`). Ключ уникальности — `(repository_coordinate, type)`.
- `scope = intent` — выход прохода агента над конкретным интентом (verification). Ключ уникальности — `(intent_id, type)`, опционально уточняется `binding_id`, когда артефакт привязан к конкретному PR/клону.

Поля агрегата (консервативно, без CMS-обобщений):

- `id`
- `scope` — `repository` | `intent`
- `repository_coordinate` — `(provider, owner, repo)` (для обоих scope контекст репо всегда известен; логическая связь по координате, как у binding/tag-preset в ADR-0024, без отдельного FK-рефактора)
- `intent_id` — `null` для repo-scoped, заполнен для intent-scoped
- `binding_id` — опц., только intent-scoped, когда артефакт относится к конкретному PR/клону
- `type` — замкнутое множество: `db_schema_map` | `diff_summary` | `static_analysis_summary` | `diagram` | `change_history_summary`
- `document` — markdown-документ (универсальный консервативный payload; mermaid/таблицы — внутри markdown, без type-специфичных структурных схем)
- `summary` — короткий заголовок для быстрого человеческого ревью («что изменилось / что рискованно»); обязателен для verification-типов
- `source_refs` — список ссылок-провенанса (commit sha, PR number, ветка, пути файлов); типизированный, но необязательный
- `source` — `agent` | `user` (карту/сводку пишет агент в сессии; пользователь правит руками)
- `version` — монотонный счётчик на ключ уникальности; optimistic concurrency через `expected_version` + typed `ApiException` (как `Intent.text`, [ADR-0002](0002-domain-model-and-text-versioning.md))
- `created_at`, `updated_at`

Полная история версий артефакта (отдельная коллекция версий, как `intent_events`) — **out of scope**; `version` здесь — concurrency-токен и провенанс, не история. Текущий артефакт на ключ — один (replace bump-ит `version`).

### MCP-поверхность (узкая, по [ADR-0030](0030-mcp-surface-policy-cli-first.md))

Обобщаем, а не плодим per-type. Это правит точечно сформулированное в ADR-0030 узкое write-исключение `write_repository_schema`:

- **Repo-scoped write** — `write_repository_artifact(provider, owner, repo, type, document, summary?, source_refs?, expected_version?)`. Заменяет именованный `write_repository_schema` (тот был частный случай `type=db_schema_map`). Единственный legitimate агентский write вне работы-над-интентом — потому что данные живут в Throne-Mongo, видимы cross-intent и иначе агенту недоступны.
- **Repo-scoped read full** — `get_repository_artifact(provider, owner, repo, type)` — дочитка полного документа перед правкой (обобщение опц. `get_repository_schema`).
- **Repo-scoped discovery** — компактные ссылки на артефакты репо (без `document`) приезжают внутри уже существующего `get_intent.repositories[]`: `[{ type, version, summary, updated_at }]`. Отдельный list-handle не вводим, пока влезает в ответ (критерий ADR-0030).
- **Intent-scoped discovery** — новая read-only секция `get_intent.artifacts[]` с теми же компактными ссылками (type, version, summary, updated_at, binding_id?). Полный документ — тем же `get_intent`-контекстом по необходимости или (если понадобится) узким read-tool, но не «на всякий случай».
- **Intent-scoped write** — путь производства verification-артефакта (агентский MCP-write vs ingest workspace-файла из `{workspace_path}/.throne/artifacts/`, ср. отложенный Slice 7) решается per-type на code review против критерия ADR-0030 «может ли агент сделать это локальным CLI/файловой операцией?». Спекулятивно сейчас не открываем — резервируем модель хранения и read-поверхность.

### HTTP / контракты ([ADR-0006](0006-openapi-contract-first-codegen.md))

Артефакты read/write через REST на сущности `Repository` (list/get/put карты с `expected_version` для ручной правки) и через intent-контекст для intent-scoped. Один набор DTO `ArtifactDto` / `ArtifactSummary` параметризуется `type`/`scope`, а не плодит DTO-на-тип.

### UI

Используем существующий extensible-реестр панелей детали интента (`apps/web/src/pages/intent-detail/model/panel-registry.tsx`, [ADR-0026](0026-embedded-terminal-capabilities-and-run-preflight.md) / commit `cb37e59`):

- Repo-scoped карта схемы рендерится в секции репозитория (inline-mermaid) — на странице интента и на странице репозитория.
- Verification-артефакты — отдельная панель в placement `review`, gate `capability: "repositories"`, добавляется одним дескриптором в реестр без правки shell. Это и есть «запланированная панель/регион под verification-артефакты» из acceptance-критерия.

### Realtime ([ADR-0008](0008-realtime-contract-first-events.md))

Одна семья событий `artifact.updated` (несёт `scope`, `type`, ключ) вместо per-type событий; фанаут стандартным domain-event pipeline. Repo-scoped `repository.schema_updated` из плана Slice 3 поглощается этим обобщением.

### Что это меняет в эпике

Slice 3 реализует `db_schema_map` **как первый `type` на оси `Artifact`**, а не как поле `Repository`. Режим `schema_map` (mini-router, ADR-0014) и его bundle остаются как способ генерации — агент пишет результат через `write_repository_artifact(type=db_schema_map, …)`. Старый Slice 7 (intent-scoped диаграммы) и verification-roadmap садятся на ту же ось сменой `type`/`scope`, без нового домена.

## Consequences

### Positive

- Новый вид артефакта = новое значение `type` (+ опц. UI-рендер и инструкция режима), а не новый домен/коллекция/HTTP-модуль/realtime-семья. Acceptance-критерий «без нового top-level домена каждый раз» выполнен by construction.
- Агент находит артефакты через уже существующий intent/repository-контекст (`get_intent.repositories[]` + `get_intent.artifacts[]`), MCP не расползается — обобщили две schema-tool'ы в две type-параметризованные.
- Длинные генерируемые выходы уходят из `Intent.text` в типизированный артефакт — история интента остаётся чистой (ADR-0019).
- Инвалидация/realtime растёт O(1): одна семья `artifact.updated`, один read-model.

### Negative / Risks

- `document`-as-markdown — намеренно слабая типизация payload'а: структурные гарантии (например, валидный mermaid erDiagram) не на уровне схемы. Mitigation: валидация per-type на write-границе там, где это даёт ценность; не выносим в общий агрегат.
- Граница repo-scoped vs intent-scoped для `diagram` неоднозначна (архитектурная диаграмма репо vs диаграмма под задачу). Mitigation: `scope` выбирается явно при создании; один `type` может существовать в обоих scope.
- Слияние «repository-» и «verification-артефактов» в один агрегат рискует протечь, если verification обрастёт сильно иным жизненным циклом (триггеры, истечение). Mitigation: триггер на пересмотр — появление у verification-артефактов поведения, не выразимого через `type` + scope (например, TTL/CI-привязка); тогда — отдельным ADR, а не растягиванием модели.
