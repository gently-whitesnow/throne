# ADR-0033: Якоря и резолюция review-комментариев

## Status

Accepted
Date: 2026-06-09
Related: [ADR-0024](0024-intent-repository-binding-and-cli-providers.md), [ADR-0030](0030-mcp-surface-policy-cli-first.md), [ADR-0032](0032-gitlab-provider.md), [ADR-0006](0006-openapi-contract-first-codegen.md)

## Context

[ADR-0024](0024-intent-repository-binding-and-cli-providers.md) сделал комментарии PR/MR provider-owned: тела не хранятся, провайдер — источник истины, Throne держит только etag/cursor на binding'е. `PullRequestCommentDto` нёс лишь `path` — без `line` / `side` / `resolved` / `thread_id`. Этого хватало на read-only ленту в карточке интента, но ревьюилка (fullscreen review workspace) должна закрывать ревью внутри Throne: показывать существующие комментарии инлайн в diff, ходить от коммента к строке, резолвить треды и удалять свои комментарии. Инлайн-композер для **новых** комментов и round-trip SHA уже сделаны Slice 4A; здесь — **чтение** анкера/резолюции существующих комментов и две новые мутации (резолв треда, удаление коммента).

Открытые вопросы: где брать `resolved` + `thread_id` у каждого провайдера; как замапить line/side на унифицированный diff-якорь; как выразить резолв/удаление provider-нейтрально, когда у gh и glab разные единицы (review thread vs discussion) и разные транспорты (graphql vs REST PUT).

## Decision

### 1. `PullRequestCommentDto` обрастает якорем и резолюцией

Добавляются nullable-поля: `line` (1-based на `side`), `side` (`ReviewCommentSide` — те же `right`/`left`, что у submit-контракта, один wire-формат строкой), `resolved` (bool), `thread_id` (string). Все nullable: issue/discussion-level комменты без diff-якоря несут `line`/`side` = null; комменты вне резолвабельного треда несут `resolved`/`thread_id` = null. `resolved` всегда читается из ответа провайдера — локального статуса Throne не держит (инвариант ADR-0024 сохраняется). Доменный `PullRequestComment` получает те же четыре опциональных поля.

### 2. Источник `resolved` + `thread_id` различается по провайдеру

**GitHub.** REST `/pulls/{n}/comments` отдаёт `line` / `side` / `original_line`, но **не** несёт ни резолюции, ни id треда — резолюция живёт только в graphql (`pullRequest.reviewThreads`). Поэтому после получения review-ленты провайдер делает один `gh api graphql`-запрос `reviewThreads(first:100){ nodes{ id isResolved comments(first:100){ nodes{ databaseId } } } }` и джойнит по `databaseId` → (`thread_id`, `resolved`). Issue-комменты (`/issues/{n}/comments`) тред-маппинга не имеют — остаются с null. Пагинация треда/комментов в треде ограничена 100/100 (MVP); превышение логируется, не обрезается молча.

**GitLab.** Discussions API уже несёт всё в одном ответе: `discussion.id` = `thread_id`, у note — `resolved` / `resolvable` и `position{new_line|old_line, new_path|old_path}`. Дополнительный запрос не нужен — обогащаем парсер.

Маппинг line/side на унифицированный якорь: `side=right` ↔ GitHub `RIGHT` / GitLab `new_line`; `side=left` ↔ GitHub `LEFT` / GitLab `old_line`. У GitHub `line` для устаревших (outdated) комментов приходит null — тогда `line`/`side` = null (показываем коммент в ленте, без инлайн-якоря).

### 3. Резолв и удаление — provider-нейтральное ядро + адаптеры

Порт `IGitProvider` расширяется двумя методами, возвращающими состояние **из ответа провайдера**:

- `ResolveReviewThreadAsync(owner, repo, number, threadId, resolved, ct) → ReviewThreadState` — GitHub: graphql-мутация `resolveReviewThread` / `unresolveReviewThread`, читаем `thread.isResolved`; GitLab: `PUT projects/:id/merge_requests/:iid/discussions/:discussion_id?resolved=…`, читаем `resolved` нот дискуссии.
- `DeleteReviewCommentAsync(owner, repo, number, commentId, threadId?, ct)` — GitHub: `DELETE /repos/{o}/{r}/pulls/comments/{id}` (threadId не нужен); GitLab: `DELETE projects/:id/merge_requests/:iid/discussions/:discussion_id/notes/:note_id` (нужен и threadId, и commentId).

HTTP-поверхность (контракт `repositories`, остаётся под UI, не MCP — ADR-0030): `DELETE …/pull-request-comments/{comment_id}?thread_id=…` и `PATCH …/pull-request-threads/{thread_id}` с телом `{resolved}` → `ReviewThreadDto{thread_id, resolved}`. PATCH-на-под-ресурс с единственным мутируемым полем — стандартная частичная правка (а не кастомный метод): тред — под-ресурс binding'а, `resolved` — его состояние.

## Consequences

- GitHub-лента комментов в ревьюилке делает +1 graphql-запрос на каждый refresh — приемлемо для ручного/поллингового пути (GET не трогает cursor — ADR-0024 § 4).
- Поллер фанаутит по новым **телам** комментов; смена только `resolved` без нового коммента фанаут не триггерит — ручной refresh подтянет актуальную резолюцию. Это осознанная граница: realtime-резолюция вне объёма.
- 100/100 лимит graphql-пагинации тредов — known limitation на крупных PR; вынос в курсорную пагинацию — отдельным слайсом по потребности.

## Alternatives considered

- **Хранить `resolved` локально на binding'е** — нарушает provider-owned инвариант ADR-0024, заводит drift между Throne и провайдером; отвергнуто.
- **Кастомный метод `:resolveThread`** вместо PATCH — стандартный update частичным телом выражает операцию без нового глагола (правило «сначала стандартные методы»); отвергнуто.
- **GitHub REST для резолюции** — REST не умеет резолвить review threads вовсе, только graphql; выбора нет.
