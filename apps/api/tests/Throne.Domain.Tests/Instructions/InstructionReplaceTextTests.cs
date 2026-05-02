using FluentAssertions;
using Throne.Domain.Instructions;
using Throne.Domain.TextVersions;

namespace Throne.Domain.Tests.Instructions;

public class InstructionReplaceTextTests
{
    private static readonly DateTimeOffset Created = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Edited = new(2026, 5, 1, 13, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "ReplaceText на пустом тексте с old_text=\"\" заполняет инициально")]
    public void ReplaceText_initial_fill_for_empty_user_instruction()
    {
        var instr = Instruction.Create(
            InstructionId.New(),
            InstructionScopeNames.User,
            "mvp-user",
            InstructionKindNames.Fix,
            text: string.Empty,
            Created);

        var result = instr.ReplaceText(
            oldText: string.Empty,
            newText: "первый текст",
            newVersionId: "ver-2",
            Edited,
            TextVersionAuthor.User);

        var replaced = result.Should().BeOfType<ReplaceInstructionTextResult.Replaced>().Subject;
        instr.Text.Should().Be("первый текст");
        instr.CurrentVersion.Should().Be(2);
        replaced.Version.Version.Should().Be(2);
    }

    [Fact(DisplayName = "ReplaceText с old_text=\"\" на непустом тексте бросает ArgumentException")]
    public void ReplaceText_empty_old_on_nonempty_text_rejected()
    {
        var instr = Instruction.Create(
            InstructionId.New(),
            InstructionScopeNames.User,
            "mvp-user",
            InstructionKindNames.Work,
            text: "текущий текст",
            Created);

        var act = () => instr.ReplaceText(
            oldText: string.Empty,
            newText: "новый",
            newVersionId: "ver-2",
            Edited,
            TextVersionAuthor.User);

        act.Should().Throw<ArgumentException>().WithMessage("*initial fill*");
    }
}
