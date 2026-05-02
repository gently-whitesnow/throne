using Throne.Domain.Instructions;

namespace Throne.Application.Instructions;

public static class InstructionBundleModeNames
{
    public const string Interview = "interview";
    public const string Work = "work";
    public const string NewProject = "new_project";
    public const string Dream = "dream";
    public const string Fix = "fix";

    public static readonly IReadOnlyList<string> All = [Interview, Work, NewProject, Dream, Fix];

    public static IReadOnlyList<string> RequiredKindsFor(string mode) => mode switch
    {
        Interview => [InstructionKindNames.Common, InstructionKindNames.Interview],
        Work => [InstructionKindNames.Common, InstructionKindNames.Work],
        NewProject => [InstructionKindNames.Common, InstructionKindNames.NewProject],
        Dream => [InstructionKindNames.Common, InstructionKindNames.Dream],
        Fix => [InstructionKindNames.Common, InstructionKindNames.Fix],
        _ => throw new ArgumentOutOfRangeException(nameof(mode), $"Unknown instruction bundle mode: {mode}."),
    };
}
