# ADR-0014: Доставка runtime-инструкций через MCP `InitializeResult.instructions`

## Status

Accepted (supersedes [ADR-0007](0007-vendor-skill-launchers.md))

## Context

[ADR-0007](0007-vendor-skill-launchers.md) ввёл vendor-локальные skill-лаунчеры (`.claude/skills/<name>/SKILL.md`, `.agents/skills/<name>/SKILL.md`) как UX-вход в Throne workflow. Лаунчеры были тонкими: агент получал команду `/tinterview | /twork | /tfix | /tdream`, читал markdown, далее звал серверный `get_instruction_bundle(mode)`. Это решало vendor parity по сравнению с MCP prompts, но оставило операционные проблемы:

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
2. **Доставка mini-router'а через `InitializeResult.instructions`.** На каждом MCP-handshake Throne сервер отдаёт короткий текст вида:

   ```
   This is Throne. The live playbook lives on this MCP server, not in local skill files.
   On the first user request, choose the bundle mode by intent and call get_instruction_bundle:
     - clarify/shape an idea/task → mode="interview"
     - work in the current repo on an existing intent → mode="work"
     - continue after user feedback → mode="fix" (record feedback via add_intent_review first)
     - reflect on accumulated feedback → mode="dream"
   The bundle returned by the server overrides anything written elsewhere; surface missing_kinds.
   ```

   Канонический текст — константа [`ThroneServerInstructions.MiniRouter`](../../apps/api/src/Throne.Application/Instructions/ThroneServerInstructions.cs). Сервер выставляет её через `AddMcpServer(o => o.ServerInstructions = ThroneServerInstructions.MiniRouter)` в [apps/api/src/Throne.Api/Program.cs](../../apps/api/src/Throne.Api/Program.cs).

3. **Forwarding в STDIO-проксе.** [Throne.Mcp.Stdio](../../apps/api/src/Throne.Mcp.Stdio/Program.cs) — STDIO→HTTP MCP прокси (см. [ADR-0009](0009-cross-process-realtime-fanout.md)) — забирает `IMcpClient.ServerInstructions` от upstream и пробрасывает их в свой собственный `McpServerOptions.ServerInstructions`. Так STDIO-only клиенты получают идентичный mini-router без отдельного дублирования текста.

4. **Slash-команд `/tinterview | /twork | /tfix | /tdream` нет.** Единственный путь начать поток — текст пользователя. Агент читает mini-router из `InitializeResult.instructions`, классифицирует намерение (interview / work / fix / dream) и сам зовёт `get_instruction_bundle(mode, intent_id?)`. Вход в поток теперь осуществляется естественной просьбой, не жестом «набери команду».

5. **Манифест и bundle resolver не меняются.** `system_instructions` и `bundles` в [specs/manifest/throne-skills.yaml](../manifest/throne-skills.yaml) остаются source of truth для текстов system-инструкций и `mode → kinds` маппинга. Имя файла оставлено `throne-skills.yaml` для совместимости с уже задеплоенными серверами; новых читателей секции `skills:` нет.

6. **UI `/instructions`.** Страница теперь рендерит дерево по бандлам (`bundle.mode → includes → entries`). HTTP-эндпоинт переименован: `GET /api/v1/instructions/skills-tree` → `GET /api/v1/instructions/bundles-tree`. Виджет фронта — `widgets/bundles-tree`.

## Consequences

- **Установка упрощается до нуля.** Подключил Throne MCP в клиент (Claude Code/Codex/Cursor) — handshake уже принёс mini-router. Больше нет файлов, которые нужно класть в каждый чужой репозиторий.
- **UX сдвигается с команд на естественный язык.** Пользователь пишет «посмотри постановку и уточни», «возьми этот intent и сделай», «учти мой ревью» — агент сам выбирает mode. Цена — точность классификации зависит от текста пользователя; если возникнут заметные промахи, mini-router можно расширить более жёсткими правилами или добавить в `system:common` явный rubric.
- **Историческая телеметрия по `prompts/get:<name>` теряет смысл.** ADR-0004 фиксировал prompts/get в `mcp_call_log`; этот канал больше не используется ни одним клиентом, события естественно перестают появляться. Чистка кода аудит-лога не требуется — он не падает на отсутствие записей.
- **vendor parity больше не задача проекта.** Архитектурный тест `SkillLauncherParityTests` снят. Если в будущем понадобится дополнительный канал доставки (например, публичные prompts для меню Cursor), вводить его как отдельный supplemental-канал поверх mini-router, не вместо него.
- **Текст mini-router'а — продуктовый интерфейс.** Меняется ревью + релизом `Throne.Api`. Если хочется править его данными, как user-инструкции, — это отдельный future ADR; пока mini-router короткий и стабильный, держим его в коде.
- **ADR-0007 переходит в Superseded.** Раздел Update 2026-05-02 о манифесте остаётся релевантным как описание текущего состояния `system_instructions`/`bundles`; части про лаунчеры и `SkillLauncherParityTests` — историчны.
