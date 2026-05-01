using Throne.Domain.Instructions;

namespace Throne.Application.Instructions;

public static class InstructionBundleModeNames
{
    public const string Interview = "interview";
    public const string LightWork = "light_work";
    public const string NewProject = "new_project";

    public static IReadOnlyList<string> RequiredKindsFor(string mode) => mode switch
    {
        Interview => [InstructionKindNames.Common, InstructionKindNames.Interview],
        LightWork => [InstructionKindNames.Common, InstructionKindNames.LightWork],
        NewProject => [InstructionKindNames.Common, InstructionKindNames.NewProject],
        _ => throw new ArgumentOutOfRangeException(nameof(mode), $"Unknown instruction bundle mode: {mode}."),
    };
}
