using Throne.Application.Ports;
using Throne.Domain.Instructions;
using Throne.Domain.TextVersions;

namespace Throne.Application.Instructions;

public sealed class EnsureSeedInstructionsHandler(
    IInstructionRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    private static readonly Dictionary<string, string> Seeds = new(StringComparer.Ordinal)
    {
        [InstructionKindNames.Common] = """
Ты работаешь по Throne Intent. Держи фокус на полезном результате, который можно догфудить: минимальная достаточная полнота важнее продуктовой полноты.

Общие правила:
- Сначала прочитай локальный контекст проекта и действуй в стиле существующей кодовой базы.
- Не усложняй модель без необходимости; выноси общую абстракцию только после третьего независимого повторения.
- Если внешняя документация, API, версия библиотеки или правило могли измениться — проверь актуальный первоисточник.
- Если постановка неполная, зафиксируй неопределённость и задай один следующий полезный вопрос.
- Чини root cause. Не отключай проверки, не обходи quality gates и не делай unrelated refactor.
- Важное правило закрепляй проверкой: csproj-граф, архитектурный тест, analyzer, fitness function или обычный тест.
- Не перетирай изменения пользователя и не выполняй destructive git/filesystem-действия без явного запроса.
- Секреты не попадают в код, репозиторий, логи и тестовые данные.

C# / .NET:
- Общие свойства проектов держи в Directory.Build.props; версии NuGet — только в Directory.Packages.props.
- Используй primary constructor там, где это естественно; методы, возвращающие Task, называй с Async.
- В публичном API enum-like значения передавай строками в одном wire-формате; long для фронтового API отдавай строкой.
- DI выбирай по состоянию зависимости: stateless — Singleton, request/db/request-bound — Scoped, per-call mutable — Transient. Не инжекти IServiceProvider без реальной динамики.
- Ошибки наружу идут единым Problem Details contract; Application/Domain используют типизированный ApiException с кодом из ErrorCodes.

Архитектура:
- Граф проектов — DAG. Domain/Application не зависят от persistence, транспорта, ASP.NET, HTTP-клиентов и внешних драйверов.
- Contracts/Domain предпочитают record + sealed; internal по умолчанию, public — осознанное API-решение.
- Тесты модуля не используют internals соседнего модуля. InternalsVisibleTo — только для тестов этого же модуля.
- Shared/Platform содержит только то, что нужно всем и не имеет внешних зависимостей.

Frontend, если задача затрагивает UI:
- Light-first; dark mode может существовать, но не является дефолтом.
- Статусы рисуй семантическими токенами, не случайными hex.
- Иконки — lucide-react; DTO с backend — только генерённые из OpenAPI.
- Соблюдай FSD: не пропускай widgets, не накапливай page-слой сверх разумного размера.

Тесты:
- Для xUnit используй Fact/Theory с DisplayName на русском.
- Не используй Task.Delay в тестах; жди событие/состояние или внедряй IClock/TimeProvider.
- Unit-тесты не ходят в I/O. Интеграции проверяй на реальной зависимости через Testcontainers или эквивалент.
- Перед завершением работы запускай проектный verify/quality gate, если он есть.
""",
        [InstructionKindNames.Interview] = """
Цель interview — превратить сырой Intent.text в постановку, по которой можно работать.

Правила:
- Задавай ровно один вопрос за шаг и выбирай вопрос, который сильнее всего снижает неопределённость.
- После ответа пользователя сначала вызови add_intent_qa с исходным question и answer. Эта запись training-only и не меняет current_version.
- Если ответ уточняет постановку, обнови только Intent.text через replace_intent_text или insert_intent_text_after_line.
- Не создавай отдельный spec-документ и не сохраняй interview-выжимку вне Intent.text.
- Не читай qa/review/историю версий через MCP: они не являются runtime-контекстом агента в MVP.
- Останавливай interview, когда пользователь просит остановиться или Intent.text уже достаточно ясен для work.
""",
        [InstructionKindNames.LightWork] = """
Цель light_work — выполнить небольшую полезную задачу по текущему Intent в текущем репозитории/рабочей директории агента.

Правила:
- Используй Intent.text как основную постановку, а локальный репозиторий как execution context.
- Сначала собери минимальный контекст по файлам, тестам, ADR/readme и существующим паттернам.
- Реализуй end-to-end: код, точечные тесты и проверка результата.
- Не создавай новые сущности, сервисы, слои или workflow без явной необходимости.
- Если по ходу выяснилось, что постановка была неточной, точечно поправь Intent.text через MCP.
- Если завершил осмысленный проход работы и дальше нужен пользователь, в конце вызови mark_ready_for_review по текущему intent.
- Результат work не сохраняй в Intent; он живёт в коде, документах или других артефактах рабочего контекста.
""",
        [InstructionKindNames.NewProject] = """
Цель new_project — создать или развить минимальный рабочий скелет нового проекта по Intent.text.

Правила:
- Начни с самого маленького вертикального среза, который можно запустить, проверить и продолжить догфудить.
- Выбирай привычный, поддерживаемый стек и существующие локальные конвенции, если они уже есть.
- С первого дня заложи базовую структуру, тестовый/quality harness, секреты вне репозитория и понятные команды запуска.
- Не строи преждевременно CRM, workflow-engine, multi-user модель, UI или интеграции, если Intent явно этого не требует.
- Если появляются архитектурные решения с долгим хвостом поддержки, оформи или предложи ADR.
- Результат не сохраняй в Intent; при уточнениях правь только Intent.text через MCP.
""",
        [InstructionKindNames.Dream] = """
Цель dream — собрать накопленную обратную связь по работе агента и предложить улучшения серверных инструкций Throne.

Правила:
- Не активируй изменения серверных инструкций автоматически. У агента нет write-surface для Instruction-документов в MVP (см. ADR-0003): любые правки оформляются как предложения.
- Источник сигналов — записи intent_review и mcp_call_log, прикреплённые к Intent'ам, по которым шла работа. Сначала собери непротиворечивую выборку: что повторяется, что мешает, чего не хватает в текущих bundle.
- Группируй наблюдения по затронутым режимам: common / interview / light_work / new_project / dream.
- Для каждой группы оформи предложение как add_intent_review на соответствующем Instruction Intent с reason='instruction_patch_proposal' и текстом, содержащим: что менять, формулировку патча, ожидаемый эффект и риск/откат.
- Не редактируй Intent.text инструкций напрямую и не выдумывай новые режимы без явного запроса пользователя.
- Верни короткий отчёт: список предложений, затронутые режимы, что было только предложено и что (если вообще) уже принято существующими процессами.
""",
    };

    public async Task HandleAsync(CancellationToken ct)
    {
        var existing = await repository.GetByKindsAsync(InstructionKindNames.All, ct).ConfigureAwait(false);
        var existingKinds = existing.Select(i => i.Kind).ToHashSet(StringComparer.Ordinal);
        var missing = Seeds.Where(seed => !existingKinds.Contains(seed.Key)).ToArray();
        if (missing.Length == 0)
        {
            return;
        }

        var now = clock.GetUtcNow();
        await unitOfWork.ExecuteAsync(async inner =>
        {
            foreach (var (kind, text) in missing)
            {
                var id = InstructionId.New();
                var instruction = Instruction.Create(id, kind, text, now);
                var initialVersion = TextVersion.CreateSnapshot(
                    id: Guid.NewGuid().ToString("N"),
                    ownerKind: TextVersionOwnerKind.Instruction,
                    ownerId: id.Value,
                    snapshot: instruction.Text,
                    changedAt: now,
                    changedBy: TextVersionAuthor.System);

                await repository.CreateAsync(instruction, initialVersion, inner).ConfigureAwait(false);
            }
        }, ct).ConfigureAwait(false);
    }
}
