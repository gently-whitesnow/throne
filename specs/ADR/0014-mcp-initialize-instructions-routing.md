# ADR-0014: Доставка runtime-инструкций через MCP `InitializeResult.instructions`

## Status

Accepted. Заменяет ранний UX-вход через vendor-локальные skill-лаунчеры (`.claude/skills/`, `.agents/skills/`) — см. Context. **Amended 2026-06-19** — см. ниже.

## Amendment (2026-06-19): standalone = knowledge base, bundle removed

Bundle-обвязка доставки плейбука демонтирована целиком (см. [ADR-0034](0034-dual-execution-contours-hooks-vs-bundles.md), [ADR-0036](0036-unify-prompt-part-entity-and-rename-mcp.md)). Конкретно для этого ADR:

- MCP-тул `get_prompt_bundle` и HTTP `getBundlesTree` (`/api/v1/prompt-parts/bundles-tree`) **удалены**. UI-страница больше не рендерит дерево бандлов.
- `ThroneServerInstructions.MiniRouter` **переписан**: Throne для standalone-агента — это база знаний интентов. Агент читает/пишет `Intent.text` и по явной просьбе меняет статус через `set_intent_status` / создаёт интент через `create_intent`. Mini-router больше не упоминает `get_prompt_bundle` и режимы `work/interview/review/dream` — это режимы embedded-контура, не standalone. Авто-переходов статуса по чтению бандла нет (их триггерил bundle-read, которого больше не существует).
- Решения 2, 4, 6 и упоминания `bundles-tree` ниже описывают историческое состояние и читаются как контекст; актуальный текст mini-router и поверхность standalone заданы этим amendment.

Манифест продолжает быть source of truth для `system_instructions` и `bundles[]`, но `bundles[]` теперь питает только embedded-композицию (seed `mode_roles`), а не доставку по MCP.

## Context

Ранний UX-вход в Throne workflow был сделан через vendor-локальные skill-лаунчеры (`.claude/skills/<name>/SKILL.md`, `.agents/skills/<name>/SKILL.md`). Лаунчеры были тонкими: агент получал команду `/tinterview | /twork | /tfix | /tdream`, читал markdown, далее звал серверный `get_instruction_bundle(mode)`. Это решало vendor parity по сравнению с MCP prompts, но оставило операционные проблемы:

1. **Команда геморна для пользователя.** Запомнить и набрать `/twork` каждый раз — дополнительный когнитивный налог.
2. **Геморна установка.** В каждый репозиторий, где работают агенты, нужно положить два набора SKILL-файлов (`.claude/skills/`, `.agents/skills/`), синхронизировать их с манифестом, подтянуть архитектурный тест парности. Без installer-а это ручная процедура; с installer-ом — лишний шаг релиза в чужие проекты.
3. **Скилы — не первоклассный canal MCP.** Они живут в файловой системе вне MCP-сессии и не доставляются автоматически тем клиентам, которые их не индексируют (Claude.ai web игнорирует, Codex CLI/Cursor имеют свои нюансы).

MCP-протокол уже даёт штатный канал для серверных инструкций: поле `instructions` в `InitializeResult` (см. MCP spec, Lifecycle / InitializeResult). Любой совместимый клиент получает этот текст в ответе на `initialize` и кладёт его в системный контекст агента. Это и есть «первое касание» сессии по определению протокола — никакого собственного отслеживания не нужно, сервер остаётся stateless.

Поддержка клиентов: Claude Code, Codex CLI, Cursor подхватывают `InitializeResult.instructions` штатно. Claude.ai web игнорирует — не целевой клиент.

## Decision

1. **Отказ от skill-лаунчеров.** Удаляются:
   - каталоги `.claude/skills/{tinterview,twork,tfix,tdream}/` и `.agents/skills/{tinterview,twork,tfix,tdream}/`;
   - секция `skills:` в [specs/manifest/throne-skills.yaml](../manifest/throne-skills.yaml) (манифест продолжает быть source of truth для `system_instructions` и `bundles`);
   - архитектурный тест парности `SkillLauncherParityTests`;
   - HTTP-операция `getSkillsTree` и поддерживавшие её `GetSkillsTreeHandler`/`SkillsTreeDto`/`SkillNodeDto` (заменены на `getBundlesTree` + `BundlesTreeDto`, потому что UI-странице `/instructions` больше нечего рендерить на уровне «скилов»).
2. **Доставка mini-router'а через `InitializeResult.instructions`.** На каждом MCP-handshake Throne сервер отдаёт короткий условный глоссарий с единственным триггером: если пользователь явно просит прочитать бандл — агент зовёт `get_prompt_bundle`. Если пользователь просто описывает задачу, mini-router не классифицирует её по смыслу и не дёргает бандл — это устраняет ложные раундтрипы во встроенном контуре, где контекст уже инъектится upfront (см. [ADR-0034](0034-dual-execution-contours-hooks-vs-bundles.md) §2). Текст вида:

   ```
   This is Throne, an MCP server for intents. The working playbook for an intent is not in local files — it comes from get_prompt_bundle.

   When the user asks to read/«прочитай» a bundle for a mode (work, interview, review, dream), call get_prompt_bundle({mode, intent_id}) and follow the text it returns — it is the source of truth. Surface any missing_keys to the user instead of improvising. intent_id comes from the message or active context; for work/interview create one via create_intent if none is given (dream runs without an intent).

   Do not call get_prompt_bundle on your own initiative when the user merely describes a task without asking to read a bundle — wait for an explicit request.
   ```

   Канонический текст — константа [`ThroneServerInstructions.MiniRouter`](../../apps/api/src/Throne.Application/Mcp/ThroneServerInstructions.cs). Сервер выставляет её через `AddMcpServer(o => o.ServerInstructions = ThroneServerInstructions.MiniRouter)` в [apps/api/src/Throne.Api/Program.cs](../../apps/api/src/Throne.Api/Program.cs).

   **Update ([ADR-0022](0022-frontier-driven-dream-flow.md)):** режим `dream` переведён на frontier-driven flow — агент сам читает свежие диалоги локально (`get_dream_sources` + `Read`/`Glob`) и предлагает патчи через `propose_prompt_part_patch`; серверный insight-pipeline демонтирован. Имя режима `dream` сохраняется ради совместимости конфигов и манифеста; меняется только содержимое bundle и набор MCP-инструментов, на которые он опирается.

3. **Прямой HTTP MCP.** После [ADR-0037](0037-direct-http-mcp-for-standalone-agents.md) standalone-клиенты подключаются к `Throne.Api /mcp` напрямую и получают mini-router из `InitializeResult.instructions` без дополнительного forwarding-процесса. Claude Desktop, которому локально нужен stdio, использует внешний bridge `mcp-remote`.

4. **Slash-команд `/tinterview | /twork | /tdream` нет.** Единственный путь начать standalone-поток — явная просьба пользователя прочитать конкретный бандл. Mini-router не классифицирует свободное описание задачи; пользователь либо пишет «прочитай бандл work/interview/dream …», либо нажимает copy-кнопку на странице интента / `/improvements`, которая кладёт ровно такую формулировку в буфер. Это сознательно жертвует «mode by meaning»-вход ради устранения лишнего MCP-раундтрипа во встроенном контуре ([ADR-0034](0034-dual-execution-contours-hooks-vs-bundles.md) §2).

5. **Манифест и bundle resolver не меняются.** `system_instructions` и `bundles` в [specs/manifest/throne-skills.yaml](../manifest/throne-skills.yaml) остаются source of truth для текстов system-инструкций и `mode → kinds` маппинга. Имя файла оставлено `throne-skills.yaml` для совместимости с уже задеплоенными серверами; новых читателей секции `skills:` нет.

6. **UI `/instructions`.** Страница теперь рендерит дерево по бандлам (`bundle.mode → includes → entries`). HTTP-эндпоинт переименован: `GET /api/v1/instructions/skills-tree` → `GET /api/v1/instructions/bundles-tree`. Виджет фронта — `widgets/bundles-tree`.

## Consequences

- **Установка упрощается до нуля.** Подключил Throne MCP в клиент (Claude Code/Codex/Cursor) — handshake уже принёс mini-router. Больше нет файлов, которые нужно класть в каждый чужой репозиторий.
- **UX сдвигается с команд на явную просьбу или copy-кнопку.** Чтобы войти в standalone-поток, пользователь либо явно просит «прочитай бандл …», либо использует copy-кнопку, которая кладёт такую формулировку в буфер. Mini-router сознательно не классифицирует свободное «посмотри постановку и уточни» по смыслу — это плата за устранение ложных MCP-раундтрипов во встроенном контуре ([ADR-0034](0034-dual-execution-contours-hooks-vs-bundles.md) §2), где контекст уже инъектится upfront и любая «describe a task» эвристика превращалась в лишнее чтение бандла. Если в будущем понадобится natural-language вход, он вернётся через явный rubric в `system:common` или per-connection вариацию, а не через эвристику в mini-router.
- **Историческая телеметрия по `prompts/get:<name>` теряет смысл.** ADR-0004 фиксировал prompts/get в `mcp_call_log`; этот канал больше не используется ни одним клиентом, события естественно перестают появляться. Чистка кода аудит-лога не требуется — он не падает на отсутствие записей.
- **vendor parity больше не задача проекта.** Архитектурный тест `SkillLauncherParityTests` снят. Если в будущем понадобится дополнительный канал доставки (например, публичные prompts для меню Cursor), вводить его как отдельный supplemental-канал поверх mini-router, не вместо него.
- **Текст mini-router'а — продуктовый интерфейс.** Меняется ревью + релизом `Throne.Api`. Если хочется править его данными, как user-инструкции, — это отдельный future ADR; пока mini-router короткий и стабильный, держим его в коде.
- **Манифест остаётся source of truth для `system_instructions`/`bundles`.** Skill-лаунчеры и их parity-тест удалены как историчные; секция `skills:` манифеста снята, остальная часть манифеста описывает текущее состояние.
