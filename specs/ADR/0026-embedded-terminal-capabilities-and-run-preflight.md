# ADR-0026: Embedded agent terminal, capability gating, tag default repositories и Run pre-flight

## Status

Accepted
Date: 2026-05-27
Related: [ADR-0006](0006-openapi-contract-first-codegen.md), [ADR-0008](0008-realtime-contract-first-events.md), [ADR-0014](0014-mcp-initialize-instructions-routing.md), [ADR-0024](0024-intent-repository-binding-and-cli-providers.md), [ADR-0025](0025-domain-aggregate-style-rich-ddd.md)

## Context

Slice 2 закрывает три боли повседневной работы в Throne: ручной `cd` в workspace, ручное связывание тегов с репозиториями и потерю сессии агента при рестарте сервера. Решения интервью (исходный проход + v2-финализация на странице Slice 2) уже зафиксировали продуктовую форму. Этот ADR фиксирует архитектурные решения, которые становятся источником правды для последующих T-задач (Domain/Mongo, Infra tmux+PTY+clone, Application+HTTP, Frontend).

Ключевые вопросы, на которые отвечает ADR:

1. Как декларируется capability-gating и какой контракт у `/api/v1/settings/capabilities`.
2. Какой контракт у embedded-terminal REST-эндпоинтов и где живёт bidirectional WebSocket-канал PTY-кадров (он не подходит под SSE-only `realtime/events.yaml` по ADR-0008).
3. Какой формат выбрать под WebSocket-контракт — AsyncAPI 2.6 или собственный yaml в `realtime/websocket/`.
4. Как расширяется `Tag` под `default_repositories` и какой формы новый эндпоинт.
5. Как Run pre-flight (`POST /terminal/run`) сосуществует с Slice 1 binding/clone-pipeline и какие гарантии stateless-идемпотентности он даёт.
6. Как доставляется состояние tmux-сессии (`session_state`) клиенту — SSE-событие или поле в ответе.
7. Что **не** меняется в Slice 2 (явные negative-spaces, чтобы T-задачи не расползались).

## Decision

### 1. Capability registry: singleton + explicit opt-in

- Capability — это запись `{ name, title, description, prerequisite_hint, detected, detection_detail?, enabled }`. Закрытое множество ключей в Slice 2: `repositories`, `terminal`, `vscode`. Будущие фичи (Jira, GitLab provider) добавляют новый ключ — расширяемо без слома контракта.
- `enabled` хранится в singleton-документе `capabilities` (один на инстанс, local-only). Default — `false` для каждого ключа: detection (`tmux --version`, `code --version`, `gh auth status`) **никогда** не флипает `enabled` автоматически.
- Read-time materialization дефолта: первый `GET /api/v1/settings/capabilities` материализует отсутствующие ключи как `enabled=false`. Никакой одноразовой миграции, никаких onboarding-toast'ов в UI.
- Detection — TTL-кэш в Application/Infrastructure, обновляется в фоне; результат публикуется в `CapabilityDto.detected` + `detection_detail`. Включить тогл без detected-prerequisite разрешено (юзер ставит tool позже); UI рисует красную пометку рядом с тоглом.
- HTTP-контракт — отдельный модуль `capabilities` в `specs/contracts/`:
  - `GET /api/v1/settings/capabilities` → `CapabilityDto[]`.
  - `PUT /api/v1/settings/capabilities/{name}` (body `{ enabled }`) → обновлённый `CapabilityDto`. 404 на unknown name.
- Retrofit Slice 1: при первом старте после деплоя Slice 2 ключ `repositories` тоже стартует в `false`. Существующие `IntentRepositoryBinding`-документы не удаляются — просто скрываются в UI до явного opt-in.

### 2. Embedded terminal REST: один эндпоинт run + один restart, никакого state на сервере

- Модуль `terminal` в `specs/contracts/`:
  - `POST /api/v1/intents/{intent_id}/terminal/run` — единственный триггер pre-flight pipeline (см. §5). Body `{ mode: work|interview|dream }`. Response 202 + `RunIntentTerminalResponse { session_name, session_state, bindings, blocking_bindings? }`.
  - `POST /api/v1/intents/{intent_id}/terminal/restart` — `tmux kill-session -t throne-{intent_id}` + повторный pre-flight. Body и response идентичны `run`. Pre-flight идемпотентен: на готовом workspace = моментальный spawn. Подтверждения «уверен прервать сессию» нет — оператор сам ответственен (одно нажатие).
- Имя tmux-сессии детерминированно: `throne-{intent_id}`. Источник правды живости — `tmux has-session -t throne-{intent_id}`, не Mongo. `Intent` **не получает** новых полей (`last_agent_run_at`, `last_session_state` и т.п.); это соответствует памяти `feedback_kiss_question_state`.
- Capability-gating включён by construction: при `capabilities.terminal.enabled=false` оба эндпоинта возвращают 422 с актуальным problem-code; UI заранее скрывает кнопки.
- Promot-template хардкоден на бэке: `Прочитай бандл {mode} и {глагол} интент {intent_id}`. UI не предоставляет textarea override (память `feedback_throne_bundle_prompt` — load-bearing фраза для MiniRouter). Команда `tmux new -ADs throne-{intent_id} -- claude "{prompt}"` запускается без `bash`-wrapper'a — tmux владеет lifecycle'ом сам (Slice 2 Q3: claude exit → tmux session исчезает).

### 3. WebSocket PTY-канал: собственный yaml `realtime/websocket/terminal.yaml` (не AsyncAPI)

Bidirectional кадры терминала не помещаются в `realtime/events.yaml` (SSE-only по ADR-0008) и не делают этого осмысленным расширением. Выбор формата зафиксирован сразу — следующие T-задачи на него опираются.

**Решение: собственный yaml `specs/contracts/realtime/websocket/terminal.yaml`** с секциями `client_to_server` / `server_to_client` + custom-codegen в T-03/T-06 при необходимости.

Pro AsyncAPI 2.6:
- Индустриальный стандарт, есть AsyncAPI Studio, генераторы TS (`@asyncapi/generator`) и C# существуют.
- Документация на канал/сервер/operation богатая.

Contra AsyncAPI 2.6:
- На один канал и три типа кадров AsyncAPI добавляет нетривиальный boilerplate (servers, channels, operations, components, messages, traits).
- TS/C# генераторы — отдельный toolchain (Java-based AsyncAPI CLI или Node), новый quality-gate и зависимости в репо.
- Throne уже имеет минимально-yaml идиому контрактов realtime (`events.yaml` + `realtime-verify-coverage.sh`); вторая форма размывает идиому.

Pro собственный yaml:
- ~50 строк под один канал vs ~100+ AsyncAPI — близко к существующему стилю.
- Парсер тривиален (читаем yaml в node-скрипте, как codegen-contracts.mjs и codegen-realtime.mjs).
- Соответствует KISS-принципу проекта (`feedback_kiss_question_state` — не вводить инфраструктуру до доказанной необходимости).

Contra собственный yaml:
- Не индустриальный стандарт — внешний tooling работать не будет.
- При появлении 3+ bidirectional каналов своя codegen-инфраструктура начнёт перевешивать.

Trigger to revisit: если slice добавляет 3-й bidirectional канал или сторонняя интеграция (например, Plannotator) затребует AsyncAPI-формат для своего канала, ADR пересматривается одним из следующих slice'ов. До тех пор — собственный yaml.

В Slice 2 codegen из этого yaml **не вводится**: типы кадров `{type, data}` / `{type, cols, rows}` достаточно простые, чтобы T-06 (фронт) и T-03/T-05 (бэк) использовали их вручную с reference на yaml. Если боль материализуется — генератор добавляется отдельным интентом.

### 4. `Tag.default_repositories[]` + `PUT /tags/{id}/default-repositories`

- Поле — массив `{ provider, owner, repo, default_branch? }`. Уникальность по `(provider, owner, repo)` внутри одного тега (нормализация на сервере: дедуп при PUT, отказ — out of scope, просто collapse).
- Расширение `tags`-модуля контракта:
  - `GET /api/v1/tags/{id}` — новая операция, возвращает `TagDetailDto` (всё что в `TagDto` + `default_repositories[]`). Старый list-эндпоинт продолжает возвращать компактный `TagDto`.
  - `PUT /api/v1/tags/{id}/default-repositories` — whole-list replace + `expected_version` (optimistic concurrency, как `renameTag`). PATCH намеренно не вводим — список маленький, whole-replace проще и не требует field-mask семантики.
- Cross-module enum/DTO duplication: NSwag не резолвит `$ref` между разными OpenAPI-документами (`../repositories/openapi.yaml#/components/...` падает на runtime). Источник правды для shared-типа — модуль-владелец (`repositories` для `GitProvider`/`CloneStatus`); потребители (`tags`, `terminal`) держат локальные «зеркала» (`TagDefaultGitProvider`, `BindingCloneStatus`) с явным комментарием «sync with ADR-0024». Mismatch ловится при PR-ревью (значения enum'ов закрыты и редко меняются — `github` сегодня единственный provider). Альтернатива — единый supergraph yaml — отклонена: нарушает module-per-folder layout (ADR-0006) и требует переписать NSwag-генераторы.

### 5. Run pre-flight: stateless-idempotent diff + parallel partial clone + blocking spawn

`POST /terminal/run` объединяет четыре шага в одном эндпоинте:

1. **Stateless auto-bind**: union `Tag.default_repositories` по всем тегам интента, дедуп по `(provider, owner, repo)`, diff против существующих binding'ов; недостающие — создаются как `IntentRepositoryBinding` в `pending` (логика Slice 1). Marker'а «уже применяли defaults» **нет** (ADR-0024 + памятка `feedback_kiss_question_state`): source of truth один (`Tag.default_repositories`), повторный Run даёт идентичный результат. Если оператор отвязал дефолт через UI, а потом снова жмёт Run — биндинг возвращается; чтобы полностью убрать репу из контекста, надо снять её с тега или снять тег с интента.
2. **Parallel partial clone**: для каждого binding'а в `pending`/`failed` запускается `gh repo clone {owner}/{repo} {path} -- --filter=blob:none` через `RepositoryCloneService` (адаптация Slice 1). Лимит параллелизма — `Throne:Clone:MaxParallel` (default 4).
3. **Blocking-ожидание `ready`**: эндпоинт ждёт, пока все binding'и интента не в `clone_status=ready`. Если что-то ушло в `failed`/`broken` — spawn не запускается, response = 202 + `session_state=blocked` + `blocking_bindings=[…]`. Прогресс per-binding оператор видит через существующий SSE-канал `intent.repository_clone_progress` (ADR-0024 / ADR-0008).
4. **Tmux spawn**: только когда всё `ready` — `tmux new -ADs throne-{intent_id} -- claude "{prompt}"` через `Throne.Terminals` (T-03). 

Bundle-read из MCP **не триггерит ничего** из этого: pre-flight единственным образом запускается явным HTTP-вызовом из UI (Run-кнопка). Это снимает «курицу-яйцо»: для `get_instruction_bundle` агент уже должен быть запущен в подготовленной cwd; правильный порядок — Run → pre-flight (workspace + clone) → spawn agent.

Параллельный Run при живой сессии возвращает 409. UI знает про `tmux has-session` (см. §6) и блокирует кнопку, оставляя `Restart` как единственный путь рестарта.

### 6. Доставка `session_state` — поле в ответе, без нового SSE-события

В Slice 2 `realtime/events.yaml` не расширяется. Оператор узнаёт о смене `session_state`:

- Из ответа `POST /terminal/run` / `restart` (поле `session_state` + `blocking_bindings`).
- На монтировании страницы интента: фронт делает `GET …/terminal/run`-эквивалент? Нет — фронт инициирует первый запрос только когда оператор жмёт Run; до этого предполагается «нет сессии». На live-странице во время ожидания клонов прогресс уже идёт через `intent.repository_clone_progress`.
- Через WebSocket-канал `/terminal/ws`: попытка attach при отсутствии сессии завершается close 1008; успешный attach подтверждает `running`.

Альтернатива — добавить новый event `intent.terminal_session_changed` в `realtime/events.yaml` и фанаутить состояние через стандартный SSE-pipeline. Отклонено в Slice 2 как преждевременная инфраструктура:

- Cross-tab consistency для single-user local-only сценария не критична: одновременная работа в нескольких вкладках с одним интентом — экзотика, тяжёлый кейс решает refresh.
- Realtime-gate (`realtime-verify-coverage.sh`) требует одновременного появления domain-event record + мэппера + frontend subscriber'а. Добавлять всю эту цепочку ради одного UI-индикатора — overhead.
- Контракт можно расширить позже без breaking changes (новое событие в yaml + handler + subscriber).

Если на проде боль материализуется (например, две вкладки расходятся по `session_state`) — добавляем event'ом в отдельном интенте.

### 7. Out of scope для T-задач (фиксируем явно)

- **`Intent`-схема не расширяется** новыми полями (`last_agent_run_at`, `last_session_state`, `auto_bound_at`, …). T-02 это явно соблюдает.
- ~~**Никакого хука «status → kill tmux»**. Переходы интента в `done`/`reject`/`fridge` не убивают живую tmux-сессию.~~ **Частично пересмотрено — см. § 8 ниже.** Переход в `done` теперь убивает сессию; `reject`/`fridge` по-прежнему не трогают её.
- **Migration-логика для `Capabilities`-aggregate не вводится** — read-time materialization дефолта `enabled=false`, никакой одноразовой миграции, никаких onboarding-toast'ов.
- **VS Code shell-out** в этом ADR не получает HTTP-контракта: эндпоинты `POST .../open-in-vscode` идут вместе с capability-gating через `capabilities.vscode` и описываются на T-05 (HTTP) / T-07 (фронт). В docker-варианте capability `vscode` детектится как `detected=false` без `vscode://`-fallback'а — кнопка просто скрывается (Slice 2 Q4 + T-07).
- **AuthN на WebSocket-канале**: Slice 2 local-only assumption (Q5). Same-origin / CSRF / token-flow выводится в отдельный интент, когда Throne станет multi-user или поедет на удалённый хост.
- **Кастомизация prompt'а**: захардкожен (Q8). Textarea-override не вводим; оператор использует Copy prompt для своего терминала.
- **MCP write-surface для terminal/capabilities/tag-defaults**: read-only by design (паттерн ADR-0024). Все мутации — UI.

### 8. Хук «intent → `done` ⇒ kill tmux» (пересматривает § 7)

Переход интента в `done` (и авто-по-мерджу PR, и ручной «закрыть как готово» оператором из UI) убивает tmux-сессию интента: `tmux kill-session -t throne-{intent_id}`, идемпотентно (нет сессии → молча ок). Реализовано доменным обработчиком на событие `IntentStatusChanged` (статус `done`), переиспользующим тот же kill-путь, что и restart-эндпоинт (§ 2). `tmux list-clients` не проверяется, даже если кто-то приаттачен.

Kill **гейтится** флагом `IntentState.CleanupLocalStateOnDone` (default `true`) — тем же, что и снос рабочей папки + trust-записей. Раньше kill был безусловным, а «оставить состояние» работало лишь побочно, не давая интенту дойти до `done`; любой другой путь в `done` убивал сессию вопреки желанию оператора. Теперь «остановка терминала» и «снос локального состояния» — две половины одного решения «teardown при завершении»: снятый флаг сохраняет и сессию, и файлы за `done`, независимо от пути в него. Снимается/ставится флаг на самом интенте; галка на ревью пишет тот же флаг (D1) и отдельно гейтит авто-закрытие на мерже (D2, `SuppressMergeAutoClose`, ADR-0024).

Почему пересматриваем § 7 «никакого хука status → kill»:

- После `done` (с включённым флагом) агентская сессия не нужна; ручной `done` — явный сигнал «работа закончена».
- Полагаться только на `claude exit` недостаточно: оператор закрывает интент из UI, а не из терминала, и сессия остаётся висеть.
- Хук — best-effort и не источник правды: живость по-прежнему определяет tmux (`has-session`), пропущенный kill самозалечивается на следующем `/intents/contexts`.

Границы решения сохраняются: `reject`/`fridge` сессию **не** трогают (только `done`); `Intent`-схема не расширяется; ограничение «никакого хука» для остальных терминальных статусов остаётся в силе.

## Alternatives

### Capability storage

- **Per-user toggle**: Throne — local-only single-user; per-user — преждевременная сложность.
- **Auto-enable on detection**: противоречит явному opt-in (Slice 2 принцип) — пользователь обязательно проходит через `/settings`.

### WebSocket контракт

- **AsyncAPI 2.6** (см. §3) — отклонено в пользу собственного минимального yaml, trigger to revisit зафиксирован.
- **Документация в OpenAPI** (через `paths` с пометкой "upgrades to ws") — ломает OpenAPI семантику (path возвращает HTTP-ответ, а не switch-протокола), не даёт места для описания client→server кадров.

### Tag defaults

- **`Tag.default_repository: { … } | null`** (singleton, не массив) — для cross-repo интентов мало; массив естественнее. Slice 2 явно подтверждает.
- **PATCH с field_mask** — пере-инженеринг для двух-трёх элементов; whole-replace проще.

### Run pre-flight

- **Auto-bind при открытии страницы интента** (warmup) — забивает диск, если оператор пробегает по интентам не работая. Run — единственный явный момент намерения, см. Slice 2 Out of scope.
- **`auto_bound_at`-marker на интенте** — превентивный state без обоснованной необходимости; KISS (`feedback_kiss_question_state`).
- **«Start anyway»-override при failed-клоне** — слишком много edge-cases; Slice 2 Out of scope.

### Session state delivery

- **Новое SSE-событие `intent.terminal_session_changed`** — отклонено как преждевременная инфраструктура (§6). Trigger to revisit зафиксирован.
- **Polling эндпоинт `GET …/terminal/status`** — дублирует ответ Run, добавляет лишний эндпоинт, не решает cross-tab проблему. Отклонено.

## Consequences

### Positive

- T-02..T-07 имеют один источник правды по контрактам и решениям. `Intent` не расширяется новыми полями (KISS), tmux владеет сессией сам, capability-gating единообразен.
- Все новые контракты — в стандартной OpenAPI + новом `realtime/websocket/`-yaml. Frontend и backend codegen-pipeline'ы (NSwag + openapi-typescript) подхватывают через те же скрипты после добавления `nswag.{capabilities,terminal}.json` и регистрации Generated-путей.
- Решение по AsyncAPI vs собственный yaml зафиксировано с pro/contra и условием пересмотра — следующие интенты не открывают вопрос заново.
- Run pre-flight stateless-идемпотентен (`tag defaults ∖ existing bindings`) — повторные клики Run на готовом workspace = моментальный spawn, без marker'ов и follow-up инвариантов.

### Negative / Risks

- **Дубликаты DTO между Generated-namespace'ами** (`GitProvider` материализуется и в `Throne.Tags.Contracts.Generated`, и в `Throne.Repositories.Contracts.Generated` — то же для `RepositoryBindingDto` в `Throne.Terminal.Contracts.Generated`). Mitigation: цена приемлема за минимальный coupling и совпадает с тем, как уже устроены ProblemDetails из `shared.yaml`.
- **Собственный yaml-формат для WebSocket — нет внешнего tooling'а**. Mitigation: формат намеренно простой (3 типа кадров), пересмотр при появлении 3-го bidirectional канала зафиксирован.
- **`session_state` без realtime-фанаута** — две одновременные вкладки могут расходиться по индикатору. Mitigation: local-only single-user, refresh-on-mount, дополнительное событие можно ввести следующим интентом без breaking change.
- **Capability `terminal` retrofit OFF после деплоя** — оператор обязан зайти в `/settings` и явно включить тогл, иначе все Slice 2 фичи не видны. Это намеренное поведение (explicit opt-in принцип); UI поправляется одним кликом.
- **Пути `realtime/websocket/`** ещё не покрыты quality-гейтом «yaml ↔ реализация». T-03/T-05/T-06 не блокируются гейтом; gate на bidirectional yaml вводится отдельным интентом, если поверхность вырастет.

## § 9. Амендмент (2026-06-21) — essentials: detection→ready (embedded-only)

Пересматривает § 1 правило «detection **никогда** не флипает `enabled`» для подмножества
essential-capability'ей.

Throne работает только в embedded-режиме (standalone — мечта, см. readme «Запуск»). В этом
контуре explicit opt-in для **обязательных** осей готовности — лишний клик: если `gh`
авторизован, а `tmux` установлен, оператор уже намерен ими пользоваться. Страница `/settings`
переехала на модель «Готовность» (панель + чек-лист «Throne готов»), где обязательные
prerequisite не показываются тумблерами вообще — значит, без авто-готовности их было бы нечем
включить.

**Решение.** Вводится закрытое множество **essentials** = `{ repositories, terminal }`
(`CapabilityNames.IsEssential`). Для них:

- `ICapabilityAvailability.IsAvailableAsync` возвращает `detected` напрямую — без проверки
  персистентного тогла. Run pre-flight gate (§ 2) и фильтр vendor-каталога идут через этот же
  сервис, поэтому «tmux есть → Run работает» без opt-in.
- `CapabilityDto.enabled` материализуется как `detected` (зеркалит авто-готовность; фронтовый
  `isCapabilityEnabled` = `enabled && detected` остаётся прежним).
- Персистентный тогл essential'а по-прежнему пишется через `PUT /capabilities/{name}`, но на
  доступность не влияет (UI его не показывает).

Опциональные фичи (`vscode`, `gitlab`, `opencode`) **сохраняют** исходную семантику § 1:
explicit opt-in (`enabled && detected`). Это compromise, а не отмена правила — детектирование
флипает готовность только там, где отсутствие фичи делает embedded-контур неработоспособным.

Готовность вендора агента (claude/codex залогинен) — отдельная ось, не capability: она живёт
на каталоге вендоров (`GET /terminal/vendors` → `login_status`), а не в реестре capability'ей.

**Rationale пересмотра § 1.** Исходное правило защищало от «detection тихо включил фичу, юзер не
ждал». В embedded-only это не риск: контур не работает без git+tmux+вендора, поэтому
detection==намерение. Граница узкая (два ключа), обратимость полная (вернуть explicit opt-in —
убрать ключи из `EssentialNames`), risk-blast — только две essential-оси.

Снимок «Negative / Risks → Capability `terminal` retrofit OFF после деплоя» этим амендментом
снимается для `terminal`/`repositories`: тогл больше не нужен.
