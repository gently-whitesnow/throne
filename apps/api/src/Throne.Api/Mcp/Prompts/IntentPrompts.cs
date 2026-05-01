// MCP wire-format requires snake_case parameter names; prompts are an API boundary.
#pragma warning disable CA1707
using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Throne.Api.Mcp.Prompts;

[McpServerPromptType]
public sealed class IntentPrompts
{
    [McpServerPrompt(Name = PromptNames.TInterview, Title = "Interview an Intent")]
    [Description("Slash command /tinterview: create or continue an Intent interview, load get_instruction_bundle(interview, ...), ask one question at a time, save qa, and refine Intent.text.")]
    public string TInterview(
        [Description("Optional existing Intent id. When supplied, make it the active Intent for this session.")] string? intent_id,
        [Description("Optional raw user intent text from the slash command. Used to create a new Intent when no active Intent exists.")] string? text)
        => Render(PromptNames.TInterview, ModeMap[PromptNames.TInterview], InterviewPlaybook, intent_id, text);

    [McpServerPrompt(Name = PromptNames.TWork, Title = "Light work on an Intent")]
    [Description("Slash command /twork: resolve an Intent, load get_instruction_bundle(light_work, ...), and perform a small task in the current working context.")]
    public string TWork(
        [Description("Optional existing Intent id. When supplied, make it the active Intent for this session.")] string? intent_id,
        [Description("Optional raw task text. Used to create a new Intent when no active Intent exists.")] string? text)
        => Render(PromptNames.TWork, ModeMap[PromptNames.TWork], WorkPlaybook, intent_id, text);

    [McpServerPrompt(Name = PromptNames.TNew, Title = "New project work on an Intent")]
    [Description("Slash command /tnew: resolve an Intent, load get_instruction_bundle(new_project, ...), and create or evolve a minimal project skeleton.")]
    public string TNew(
        [Description("Optional existing Intent id. When supplied, make it the active Intent for this session.")] string? intent_id,
        [Description("Optional raw project intent text. Used to create a new Intent when no active Intent exists.")] string? text)
        => Render(PromptNames.TNew, ModeMap[PromptNames.TNew], NewProjectPlaybook, intent_id, text);

    [McpServerPrompt(Name = PromptNames.TReview, Title = "Review and continue work")]
    [Description("Slash command /treview: save a review note for an Intent, optionally refine Intent.text, load light_work instructions, and continue the fix.")]
    public string TReview(
        [Description("Optional existing Intent id. When supplied, make it the active Intent for this session.")] string? intent_id,
        [Description("Review note from the user. If no active Intent exists, derive a repair Intent from this text.")] string? text)
        => Render(PromptNames.TReview, ModeMap[PromptNames.TReview], ReviewPlaybook, intent_id, text);

    public static IReadOnlyDictionary<string, string> ModeMap { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [PromptNames.TInterview] = "interview",
        [PromptNames.TWork] = "light_work",
        [PromptNames.TNew] = "new_project",
        [PromptNames.TReview] = "light_work",
    };

    private static string Render(string promptName, string mode, string playbook, string? intentId, string? text)
    {
        var ctx = BuildContextBlock(intentId, text);
        return $$"""
{{SharedRules}}

## Команда
Эта инвокация — slash-команда `/{{promptName}}` (mode = `{{mode}}`).

{{ctx}}

## Playbook
{{playbook}}
""";
    }

    private static string BuildContextBlock(string? intentId, string? text)
    {
        var hasId = !string.IsNullOrWhiteSpace(intentId);
        var hasText = !string.IsNullOrWhiteSpace(text);

        if (hasId && hasText)
        {
            return $"""
## Контекст вызова
- Active Intent для сессии: `{intentId}` — работай по нему и запомни его как текущий.
- Дополнительный текст пользователя: ```
{text}
```
""";
        }

        if (hasId)
        {
            return $"""
## Контекст вызова
- Active Intent для сессии: `{intentId}` — работай по нему и запомни его как текущий.
""";
        }

        if (hasText)
        {
            return $"""
## Контекст вызова
- intent_id не передан. Исходный текст команды:
```
{text}
```
- Если в сессии уже есть active Intent — работай с ним. Иначе создай новый Intent из этого текста (`create_intent`) и сделай его active.
""";
        }

        return """
## Контекст вызова
- Аргументы не переданы. Применяй правило active-resolution: если в сессии уже есть active Intent — работай с ним; иначе попроси пользователя одной фразой описать намерение, чтобы создать Intent.
""";
    }

    private const string SharedRules = """
# Throne — slash-command runtime

Ты — coding-agent, использующий MCP-сервер `Throne` для хранения намерений пользователя (`Intent`) и runtime-инструкций (`Instruction`). Всё взаимодействие с Throne идёт через 9 MCP tools этого сервера. Ниже общие правила, затем playbook конкретной команды.

## Instruction bundle
- Перед содержательной работой вызови `get_instruction_bundle(mode, intent_id?)` для текущего режима и применяй тексты `common` + mode-specific как инструкции пользователя.
- Если `intent_id` ещё неизвестен, можно получить bundle без него; после создания/выбора Intent используй этот `intent_id` в дальнейших вызовах.
- Если `missing_kinds` не пустой, кратко сообщи пользователю, каких seed-инструкций не хватает, и продолжай только если безопасно.

## Active Intent resolution
1. Если команда содержит явный `intent_id` — работай с этим Intent и делай его active для сессии.
2. Если `intent_id` не передан, но в сессии уже есть active Intent — работай с ним.
3. Если `intent_id` не передан и active Intent отсутствует — создай новый Intent из переданного `text` через `create_intent`.
4. Если `text` тоже пуст или его недостаточно для осмысленного Intent — задай **один** уточняющий вопрос пользователю.

## Optimistic concurrency
- Каждый write-tool требует `expected_version`. Бери его из последнего `get_intent` / `read_intent_text` или из write-результата.
- На `intent.version_conflict` — перечитай Intent через `get_intent` и повтори операцию **один раз**. Если снова конфликт — спроси пользователя.
- `add_intent_qa` и `add_intent_review` проверяют `expected_version`, но НЕ инкрементируют его (qa/review — не правка text).
- После успешного `replace_intent_text` / `insert_intent_text_after_line` считай возвращённый `current_version` новым актуальным version.

## Edit discipline (file-like редактирование)
- Не пересылай весь `Intent.text` при правке.
- Для точечной правки: `replace_intent_text(expected_version, old_text, new_text)`. `old_text` должен встречаться в текущем тексте РОВНО ОДИН раз byte-exact (whitespace, переносы, BOM значимы). Пустой `new_text` допустим — это удаление.
- Для вставки: `insert_intent_text_after_line(expected_version, after_line, insert_text)`. `after_line=0` — вставка в начало, `after_line=total_lines` — append.
- Для больших документов сначала `search_intent_text(query)` или `read_intent_text(start_line, line_count)` диапазонами; серверный лимит ответа `read_intent_text` — 64 000 символов, для пагинации используй `next_start_line`.
- Не делай «полную перезапись» через `replace_intent_text` со всем текстом в `old_text` — переформулируй через несколько точечных правок.
- Сохраняй авторский смысл пользователя. Не превращай Intent.text в лог твоих действий или отчёт о выполнении.

## Error catalogue
- `intent.version_conflict` → re-read `get_intent` & retry один раз.
- `intent.text.match_not_found` → расширь `old_text` уникальным контекстом или сначала `search_intent_text`.
- `intent.text.match_ambiguous` → добавь окружение к `old_text`, чтобы фрагмент стал уникальным.
- `intent.text.line_out_of_range` → перечитай `total_lines` через `read_intent_text` и пересчитай `after_line`.

## Tags на create_intent
- Если пользователь не передал теги, попробуй определить имя текущего репозитория/рабочей директории и положи его первым тегом.
- Если уверенно не определяется — оставь массив пустым.

## Запреты
- Не вызывай write-tools для Instruction — их в MVP нет, инструкции редактирует пользователь напрямую.
- Не сохраняй артефакты `work` внутри Intent — результат живёт в репозитории.
- Не пытайся читать историю версий, qa или review через MCP — read API не выставлен (это training-only данные).
- В режиме interview не задавай больше одного вопроса за шаг.
""";

    private const string InterviewPlaybook = """
1. Тяни `get_instruction_bundle(mode = "interview", intent_id?)` и применяй seed/user-инструкции `common` + `interview`.
2. Resolve Intent по правилам выше. Если Intent создаётся через `create_intent` — добавь `tags` (хотя бы тег с именем репозитория, если он надёжен).
3. Прочитай `Intent.text`. Для больших документов — `read_intent_text` диапазонами или `search_intent_text` для конкретных фрагментов.
4. Задай пользователю **один** полезный вопрос: самый дешёвый вопрос, который сильнее всего снизит неопределённость. После ответа:
   - вызови `add_intent_qa(intent_id, expected_version, question, answer)` — это decoupled-запись, она НЕ меняет text;
   - выполни одну или несколько правок `Intent.text` через `replace_intent_text` / `insert_intent_text_after_line`, если ответ требует изменения постановки.
5. Повторяй цикл «вопрос → ответ → qa + правки» до тех пор, пока пользователь не скажет «хватит» или Intent не станет достаточным для work.
6. Не создавай отдельный spec-документ. Редактируется только `Intent.text`.
""";

    private const string WorkPlaybook = """
1. Тяни `get_instruction_bundle(mode = "light_work", intent_id?)` и применяй `common` + `light_work`.
2. Resolve Intent. Прочитай `Intent.text` (целиком через `get_intent` или диапазонами через `read_intent_text` для больших).
3. Выполняй задачу в текущем репозитории/рабочей директории — это execution context агента, Throne его не хранит.
4. Доведи работу до проверяемого результата: код/артефакты, релевантные тесты и локальная проверка, если она доступна.
5. Результат `work` НЕ сохраняй внутри Intent. Артефакты живут в коде/репозитории.
6. Если в процессе понял, что постановка не точна — точечно поправь `Intent.text` (`replace_intent_text` / `insert_intent_text_after_line`).
""";

    private const string NewProjectPlaybook = """
1. Тяни `get_instruction_bundle(mode = "new_project", intent_id?)` и применяй `common` + `new_project` — там ожидается стек, архитектурные предпочтения, правила разработки.
2. Resolve Intent. Прочитай `Intent.text`.
3. Создай минимальный рабочий скелет проекта в текущем репозитории — достаточный для запуска, проверки и следующей итерации dogfooding.
4. Добавь базовые команды запуска/проверки и только те абстракции, которые нужны первому вертикальному срезу.
5. Результат не сохраняй внутри Intent. Если постановка уточняется по ходу — правь `Intent.text` точечно.
""";

    private const string ReviewPlaybook = """
1. Resolve Intent. Если active Intent отсутствует — создай новый Intent через `create_intent` со смыслом из переданного замечания (`text`).
2. Сохрани замечание: `add_intent_review(intent_id, expected_version, note, reason)`. `note` — само замечание, `reason` — почему это важно / что AI понял неправильно.
3. Если замечание усиливает постановку — точечно поправь `Intent.text` (`replace_intent_text` / `insert_intent_text_after_line`).
4. Тяни `get_instruction_bundle(mode = "light_work", intent_id)` и применяй `common` + `light_work`.
5. Продолжи исправление в текущем репозитории. Результат `work` не сохраняй в Intent.
6. Если замечание неполное и безопасно действовать нельзя — задай **один** уточняющий вопрос.
""";
}

public static class PromptNames
{
    public const string TInterview = "tinterview";
    public const string TWork = "twork";
    public const string TNew = "tnew";
    public const string TReview = "treview";

    public static IReadOnlyList<string> All { get; } = [TInterview, TWork, TNew, TReview];
}
