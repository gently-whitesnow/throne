# ADR-0051: Стартовый seed user-частей промпта + отдельный seed-манифест

## Status

Accepted
Date: 2026-06-29
Разворачивает решение «`PromptPartSeeder` удалён / user-части создаются только явно» из
[ADR-0036](0036-unify-prompt-part-entity-and-rename-mcp.md) (Amendment 2026-06-21): стартовый
seed возвращается, но как идемпотентный seed-only-on-empty из отслеживаемого манифеста.

## Context

После [ADR-0036](0036-unify-prompt-part-entity-and-rename-mcp.md) `system`-части
синтезируются из манифеста, а `user`-части — runtime-данные в `prompt_parts`, создаваемые
только явно (UI / patch-apply); стартовый `PromptPartSeeder` был удалён. На чистой БД это
даёт пустой старт: `prompt_parts(scope=user)` пуста, поэтому mandatory user-слоты бандлов
(`common`/`work`/`interview`/`review`/`dream`) в `PromptCompositionResolver` молча
пропускаются (`GetByScopeKeyAsync` → null → `continue`), а `/agent-context` и `/improvements`
пусты. Пользователю не от чего оттолкнуться — нет ни ядра, ни примеров модульных частей.

Прежний `PromptPartSeeder` убрали потому, что он реконсайлил и `system`-копии в хранилище, и
дрейфовал относительно манифеста. Нужен seed, который: (1) заполняет только `user`-часть, (2)
срабатывает строго на истинно первом запуске и не воскрешает удалённое / не доливает в
работающие инстансы, (3) описан в отслеживаемом источнике, отдельном от system-манифеста с его
read-only-семантикой.

## Decision

### Отдельный seed-манифест

`specs/manifest/throne-user-prompt-seed-parts.yaml` — единственный источник правды
seed-набора. По части несёт `{key, text, description?, mode_roles[]}`; `mode_roles` зеркалит
доменную модель `PromptPart` (`{mode, role, order}`). Намеренно отделён от
`throne-system-prompt-parts.yaml`: system-тексты read-only и меняются PR-ом, а seed-тексты —
generic-заготовки, которые после сидинга становятся обычными редактируемыми user-частями.
Парсинг/валидация (`UserPromptSeedParser`) переиспользуют доменные инварианты
(`PromptPart.ValidateModeRoles`), так что seed не может описать часть, которую агрегат отверг
бы. Файл едет рядом с бинарём через `Content`/`Link` в `Throne.Api.csproj` (как system-манифест).

### Идемпотентный seeder, seed-only-on-empty

`UserPromptSeedSeeder` (hosted-сервис по образцу `SkillModeDefaultSeeder`) читает манифест и
пишет в реальный EF-стор (`EfPromptPartRepository`), а не в manifest-backed обёртку — строки
ложатся как настоящие editable user-данные. Идемпотентность по пустоте: сид пишет набор
только когда `prompt_parts(scope=user)` полностью пуста (истинно первый boot). Любая
существующая user-часть → no-op: сид не воскрешает удалённое и не доливает новое в работающие
инстансы. Проверка пустоты и запись — в одной `IUnitOfWork.ExecuteAsync` (атомарно).

### Содержимое сида

- Core (`common`/`work`/`interview`/`review`/`dream`) — generic-заготовки с `mandatory`-ролями,
  совпадающими с `bundles[].includes` system-манифеста (заполняют те самые пустые слоты бандла).
- Модульные примеры (`analysis`/`commit`/`tests`/`contracts`) — `default_off` (доступны, не
  выбраны по умолчанию): абстрактные образцы формата модульной части.

### Вынос преференциальных кусков system → user-seed

Жёсткий механизм остаётся в system (границы workspace, абсолютные пути, write-path, схема
`review_recommendation`, CLI-провайдеры ревью, dream-механика). Преференциальные (мнение, а не
механизм) куски перенесены в редактируемый user-seed, правкой обоих манифестов (минус в system,
плюс в seed):

- interview: «вопросы, сильнее всего снижающие неопределённость» → seed `interview`.
- work: «не плоди новые сущности/слои/workflow» и «повторный проход — только проблематизируемое,
  без unrelated-рефактора» → seed `work`.
- dream: «объём задаёт оператор — не бери больше, чем просили» → seed `dream`.

## Аудит «лишнего на старте» (вывод, без удаления)

Что реально исполняется как hosted-сервис на старте (grep `AddHostedService`), и нужно ли оно
на первом запуске:

- `EfSchemaInitializer` — `MigrateAsync` + WAL на критическом пути старта. **Нужен** (без схемы
  запросы падают). Не дублируется.
- `SkillModeDefaultSeeder` — `UpsertMissing` дефолтов skill×mode из каталога, set-on-insert.
  **Нужен** на первом запуске; на последующих идемпотентен (operator-правки не перетирает).
- `UserPromptSeedSeeder` (этот ADR) — **нужен** на первом запуске; дальше no-op.
- `IntentAttachmentCompressionWorker` — периодический `BackgroundService`, не сидер; на чистой
  БД просто простаивает (или выключен при `poll_interval<=0`). Не first-run-сущность, не
  избыточен.

Ключевая поправка к постановке: `ClaudeTrustSeeder` и `CodexTrustSeeder` **не** стартовые
сидеры — это `IWorkspaceTrustSeeder`, вызываемые per-spawn из `WorkspaceTrust.Seed(workspacePath)`
при запуске терминала в workspace, а не из `AddHostedService`. На старте они ничего не делают,
аудировать/подрезать на старте там нечего.

Явного дублирования между стартовыми сервисами не обнаружено. Удаление/правка чего-либо из
найденного — вне объёма этого ADR (отдельный интент).

## Consequences

### Positive

- Чистый первый запуск даёт заполненный `/agent-context`: ядро + примеры модульных частей.
- Seed-набор отслеживаем и отделён от read-only system-манифеста; меняется обычным PR.
- Seed-only-on-empty не угрожает работающим инстансам (ни воскрешения, ни доливки).

### Negative / Risks

- Два манифеста вместо одного — но с разной семантикой (read-only system vs seed-once user),
  поэтому слияние было бы хуже.
- Seed не покрывает re-seed уже работающих инстансов и пользовательское переопределение набора
  в своём клиенте — осознанно вне объёма.

### Out of scope

- Удаление найденного «лишнего» на старте (отдельный интент).
- Пользовательское переопределение seed-набора и re-seed работающих инстансов.
