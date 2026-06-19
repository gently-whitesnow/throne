# ADR-0030: MCP surface policy — CLI-first, context-read + редкие agent-authored writes

## Status

Accepted
Date: 2026-06-06
Related: [ADR-0003](0003-mcp-text-editing-semantics.md), [ADR-0014](0014-mcp-initialize-instructions-routing.md), [ADR-0024](0024-intent-repository-binding-and-cli-providers.md), [ADR-0026](0026-embedded-terminal-capabilities-and-run-preflight.md)

## Context

«Единое окно цикла разработки» добавляет всё больше внешних операций: git-provider (PR, комментарии, clone), файловая система workspace, terminal, capabilities, будущие интеграции. Соблазн на каждую такую операцию завести по MCP-tool'у (`list_intent_pr_comments`, `bind_repository`, `run_terminal`, …) ведёт к расползанию MCP-surface: десятки tool'ов, дублирующих то, что у агента уже есть под рукой через локальный CLI, и каждый — новый способ случайно сломать workspace.

Эта анти-паттерн уже всплыл точечно: `list_intent_pr_comments` был заведён как продуктовая поверхность, хотя PR-комментарии агент читает CLI-провайдером (`gh`/`glab`) прямо в workspace (см. [ADR-0024](0024-intent-repository-binding-and-cli-providers.md) § 8). ADR-0003 (write-tools для Instruction убраны), ADR-0024 § 8 и ADR-0026 (terminal/capabilities/tag-defaults read-only by design) приходили к одному и тому же выводу по отдельности. Этот ADR кодифицирует общий принцип, чтобы будущие roadmap-фичи не переоткрывали вопрос.

## Decision

MCP-surface Throne сознательно узкий. Tool попадает в MCP только если проходит обе проверки:

1. **Context-read** — отдаёт агенту компактный контекст интента, которого у него нет другим путём: текст и версии интента, граф связей, attachments, instruction-bundle, dream-sources, binding-метаданные (`get_intent.repositories[]`). Чтения проектируются компактными; отдельный list-handle вводится только при реальной проблеме context-window / пагинации, а не «на всякий случай» (например, отдельный `list_intent_repositories` не нужен, пока `get_intent.repositories[]` влезает в ответ).
2. **Rare agent-authored write** — запись, которая является именно «работой агента над интентом» и порождается агентом, а не пользователем: правки текста интента (ADR-0003), статус-переходы, link-граф, dream-proposals (`propose_instruction_patch`), захват runtime-артефактов (`write_repository_document` — узкое исключение для страниц знаний репозитория). Такие writes редки и не дублируют shell.

Всё остальное — **CLI-first**. Shell / git / git-provider / OS / repo-операции агент делает локальным CLI в workspace (`git`, `gh`, `glab`, файловые операции, terminal), а не через MCP-tool:

- PR review-комментарии — `gh`/`glab` или собранный в UI prompt-контекст, не durable local store и не MCP-tool.
- Bind / unbind / sync репозитория, clone, fetch — продуктовые действия пользователя в UI или CLI агента, не MCP write-surface (ADR-0024 § 8).
- Terminal / capabilities / tag-defaults — read-only by design (ADR-0026).

Критерий для нового MCP-tool, который должен пройти в code review: «может ли агент сделать это локальным CLI/файловой операцией в workspace?» Если да — tool не заводим. MCP добавляет ценность там, где данные живут в Throne (Mongo, instruction-manifest) и иначе агенту недоступны.

## Consequences

### Positive

- MCP-surface остаётся обозримым и аудируемым; меньше способов агенту случайно сломать workspace.
- Roadmap-фичи получают готовый критерий и не плодят tool-на-операцию; решение принимается на code review, а не каждый раз заново.
- Секрет-менеджмент и rate-limit остаются в вендорном CLI (ADR-0024), Throne их не дублирует.

### Negative / Risks

- Агент обязан иметь рабочий CLI (`gh`/`glab`/`git`) в среде workspace; без него часть контекста (PR-фидбек) недоступна. Mitigation: capability-индикаторы (ADR-0026) и preconditions демо (ADR-0024).
- Граница «context-read vs CLI-first» местами субъективна (например, будущие read-модели). Mitigation: спорные случаи разбираются на code review против критерия выше; при появлении реального context-window / пагинации повода — отдельным ADR.
