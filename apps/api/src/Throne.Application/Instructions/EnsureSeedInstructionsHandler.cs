using Throne.Application.Ports;
using Throne.Domain.Instructions;
using Throne.Domain.TextVersions;

namespace Throne.Application.Instructions;

public sealed class EnsureSeedInstructionsHandler(
    IInstructionRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    private static readonly IReadOnlyDictionary<string, string> Seeds = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [InstructionKindNames.Common] = "Работай минималистично. Не усложняй модель без необходимости. Предпочитай dogfooding completeness over product completeness. Если есть неопределённость, явно зафиксируй её и задай следующий полезный вопрос.",
        [InstructionKindNames.Interview] = "Задавай по одному вопросу. После ответа пользователя обновляй Intent.text через MCP и сохраняй question/answer в Intent.qa. Не создавай отдельный spec-документ: редактируй только Intent.text.",
        [InstructionKindNames.LightWork] = "Работай в текущем репозитории/рабочей директории агента. Используй Intent.text как основную задачу. Не создавай лишние сущности и не сохраняй результат work в Intent.",
        [InstructionKindNames.NewProject] = "Работай в текущем репозитории/рабочей директории агента. Используй Intent.text как постановку для нового проекта. Создай минимальный рабочий скелет, достаточный для следующей итерации dogfooding.",
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
