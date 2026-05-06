# ADR-0013: MCP attachment delivery via per-type tools (no resources)

## Status

Accepted — 2026-05-06.

## Context

Канал доставки image- и text-аттачей агенту через MCP-сервер `Throne.Api` ломается уже второй раз.

**Итерация 1.** Был tool `get_intent_attachment_image`, возвращавший `ImageContentBlock` внутри `CallToolResult.Content`. Реализация шла через `TextContentBlock` с base64 строкой → один аттач = ~25 000 текстовых токенов в tool-result. Большие скриншоты упирались в context budget агента.

**Итерация 2** (intent `7c8e0237aed44e76884cd082e99fc658`, status done). Перевели отдачу на MCP Resources: `intent://{id}/attachments`, один resource на интент, `resources/read` возвращал массив `BlobResourceContents` с per-attachment под-URI. Tool удалили.

В реальности этот канал не работает в массовых клиентах:

- MCP-spec не предписывает auto-inject resources в модельный контекст; клиент сам решает, что с ними делать.
- Claude Code, Claude Desktop, Codex, Cursor, OpenCode реализуют resources как @-mention surface для пользователя. Ни один не подкладывает resources в контекст автоматически. У агента в Claude Code в списке tools нет `read_resource` или эквивалента.
- Throne продуктово ожидает обратного UX: пользователь аплоадит файл в интент, чтобы агент **сам** его подхватил, без ручного @-mention.
- `resources/list` без скоупа стоил O(intents × attachments) на каждый клиентский запрос и дублировал метаданные, которые уже отдаёт `get_intent`.

Дополнительные исходные данные:

- Anthropic vision: ImageContentBlock в tool-result у Claude/Codex/Cursor попадает в native vision-pipeline; tarification — vision-токены ≈ width×height/750. Лимит на блок — 5 MB, max edge 1568 px (Sonnet/Haiku) или 2576 (Opus 4.7).
- `IntentAttachmentCompressionWorker` (intent `6d25adc926384dfb9732f8022a0feb5a`, ADR не выпускали) уже даёт server-side downscale до 1024 px max + JPEG q75 → любой image влезает в 5 MB-лимит.
- Anthropic prompt caching ставится клиентом, не сервером. Один image на один tool-result даёт точечный кэш-префикс на следующих витках; смешивание image+text в одном tool-result ломает локальность кэша.
- Bundle (`get_instruction_bundle`) продуктово зафиксирован как канал инструкций/промптов, не данных интента — трогать его для аттачей запрещено пользователем.

## Decision

**Tools-only канал доставки. MCP Resources провайдер удаляется целиком.**

Архитектура:

1. **Discovery** — обогащённый `get_intent.attachments[]`. На каждом аттаче поля:
   - `kind`: `"image" | "text" | "unsupported"` (resolver `AttachmentKindResolver` в `Throne.Application/Intents/Attachments`).
   - `recommended_tool`: `"read_intent_attachment_image" | "read_intent_attachment_text" | null`.
   - `is_compressed_image`, `compressed_width`, `compressed_height` — чтобы агент мог оценить vision-стоимость до чтения.
2. **Pull** — два MCP tools, по одному на content-family:
   - `read_intent_attachment_image(intent_id, attachment_id)` → `CallToolResult` с одним `ImageContentBlock { Data: base64, MimeType }`. `UseStructuredContent = false`. Hard-cap 5 MB после чтения.
   - `read_intent_attachment_text(intent_id, attachment_id, offset?, max_chars?)` → `IntentAttachmentTextSlice` с полями `content_type`, `total_size_bytes`, `returned_bytes_start`, `returned_bytes_end`, `truncated`, `text`. `UseStructuredContent = true`. `offset` в байтах, `max_chars` в символах (default 50 000, абсолютный max 200 000). UTF-8-aware дроп partial-rune в начале при `offset > 0`. При `truncated=true` агент дочитывает следующим вызовом с `offset = returned_bytes_end`.
3. **MCP Resources провайдер удаляется**: класс `IntentAttachmentsResources`, регистрация в `Throne.Api/Mcp/ThroneToolsBootstrap.cs`, `WithListResourcesHandler` / `WithReadResourceHandler` в `Throne.Api/Program.cs`, тесты `IntentAttachmentsResourcesTests`. URI scheme `intent://` уходит вместе с провайдером.
4. **Bundle не трогаем**.
5. **Ownership** наследуется автоматически через `IIntentRepository` / `IIntentAttachmentRepository` (см. ADR-0012).
6. **Не-image, не-text** (например, `application/pdf`) → `kind="unsupported"`, оба tool-а отвечают `validation.failed` с подсказкой. Поддержка PDF — отдельная итерация.

## Consequences

Положительные:

- Image идёт нативным vision-блоком у каждого tool-вызова → ~600–1300 vision-токенов на 1024 px, а не десятки тысяч text-токенов.
- Text-аттач читается чанками с понятными `offset`/`max_chars`/`truncated` — большие логи не ломают context.
- Один image на tool-result → точечный prompt-cache; смены kind не инвалидируют кэш других аттачей.
- Канал ровно один — нечего рассинхронизировать, discovery в одном месте (`get_intent`).
- `resources/list` больше не делает O(intents × attachments) на каждый клиентский опрос.

Отрицательные:

- Два tool-имени для агента вместо одного. Mitigated через `recommended_tool` в метаданных аттача — агенту не нужно «угадывать», какой звать.
- Потеряна возможность @-mention аттачей в Claude Desktop / MCP Inspector. Если такой UX понадобится — отдельный ADR с возвращением resources surface (с per-attachment URI).

## Alternatives considered

1. **Resources-only** (текущая итерация). Отвергнуто: ни один из массовых MCP-клиентов не auto-injectит resources в модельный контекст, агент аттачи не видит. @-mention — пользовательский флоу, который продуктово не нужен.
2. **Slim resources mirror рядом с tools** (план до правки). Отвергнуто как YAGNI: discovery уже в `get_intent`, @-mention сценарий не используется (пользователь специально аплоадит файл, чтобы агент подхватил автоматически — это противоположно ручному @). Если завтра понадобится — отдельный ADR.
3. **Единый универсальный `read_intent_attachment(intent_id, attachment_id)` с диспатчем по mime**. Отвергнуто: один tool-result периодически меняет тип content-блока (image ↔ text) — это бьёт по prompt-caching и type-narrowing у клиента.
4. **Bundle injection** (image-блоки внутри `get_instruction_bundle`). Отвергнуто продуктово: bundle живёт за prompts/instructions, не данные интента.
5. **Batch `read_all_attachments`**. Отвергнуто: один image на один tool-result — основа точечного кэширования; batch создаёт супер-блоб, который кэш-инвалидируется при любом изменении одного из вложений.

## Acceptance

- `dotnet test tests/Throne.Api.Tests/...` — зелёный, включая новый `IntentAttachmentToolsTests` (image happy-path, image на text-аттаче → validation, > 5 MB → too_large, text усечение по `max_chars`, продолжение по `offset`, UTF-8-aware при partial-rune offset, text на image → validation, отсутствующий intent/attachment → not_found, валидация `max_chars > 200_000`).
- `bash scripts/quality/verify.sh` → `ALL GATES PASSED`.
- Smoke в любом MCP-клиенте: после `get_intent` агент видит `kind`/`recommended_tool` и сам зовёт правильный tool без @-mention.
- `resources/list` MCP-вызов больше не отвечает (handler не зарегистрирован).
- ADR-0013 присутствует, `specs/ADR/REGISTRY.md` содержит ссылку.

## Migration

- Удалены: `apps/api/src/Throne.Api/Mcp/Resources/IntentAttachmentsResources.cs`, папка `Mcp/Resources/`, тест `IntentAttachmentsResourcesTests.cs`, `WithListResourcesHandler`/`WithReadResourceHandler` в `Program.cs`, `services.AddSingleton<IntentAttachmentsResources>()` в `ThroneToolsBootstrap.cs`.
- Добавлены: `Throne.Application/Intents/Attachments/AttachmentKind.cs`, `Throne.Api/Mcp/Tools/IntentAttachmentTools.cs`, `Throne.Api.Tests/Mcp/IntentAttachmentToolsTests.cs`.
- Изменены: `McpIntentReadModels.cs` (новые поля DTO), `IntentTools.GetIntent` (description + проекция), `ThroneToolsBootstrap.cs` (регистрация нового tool-класса).
- Legacy tool `get_intent_attachment_image` уже отсутствовал в коде до этого ADR.

Backwards-compat не нужен — Throne сейчас self-hosted single-tenant, прод-клиентов с зависимостью на `intent://`-resources нет.
