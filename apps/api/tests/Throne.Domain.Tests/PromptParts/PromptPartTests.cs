using FluentAssertions;
using Throne.Domain.Instructions;
using Throne.Domain.PromptParts;

namespace Throne.Domain.Tests.PromptParts;

public class PromptPartTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    private static PromptPart NewPart(string text, params PromptPartModeRole[] roles) => PromptPart.Create(
        PromptPartId.New(), InstructionScopeNames.User, "architecture", text, "desc", roles, Now);

    [Fact(DisplayName = "Create задаёт версию 1 и сохраняет роли")]
    public void Create_initializes_version_and_roles()
    {
        var part = NewPart("body", new PromptPartModeRole(PromptPartModeNames.Work, PromptPartRoleNames.DefaultOn, 2));

        part.CurrentVersion.Should().Be(1);
        part.Scope.Should().Be(InstructionScopeNames.User);
        part.ModeRoles.Should().ContainSingle()
            .Which.Should().Be(new PromptPartModeRole(PromptPartModeNames.Work, PromptPartRoleNames.DefaultOn, 2));
    }

    [Fact(DisplayName = "ReplaceText заменяет уникальную подстроку и бампит версию")]
    public void ReplaceText_replaces_unique_substring()
    {
        var part = NewPart("alpha beta");

        var result = part.ReplaceText("beta", "gamma", Now.AddMinutes(1));

        result.Should().BeOfType<ReplacePromptPartTextResult.Replaced>();
        part.Text.Should().Be("alpha gamma");
        part.CurrentVersion.Should().Be(2);
        part.UpdatedAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact(DisplayName = "ReplaceText на неоднозначной подстроке возвращает MatchAmbiguous без мутации")]
    public void ReplaceText_returns_ambiguous()
    {
        var part = NewPart("x x");

        var result = part.ReplaceText("x", "y", Now);

        result.Should().BeOfType<ReplacePromptPartTextResult.MatchAmbiguous>();
        part.Text.Should().Be("x x");
        part.CurrentVersion.Should().Be(1);
    }

    [Fact(DisplayName = "ReplaceText на отсутствующей подстроке возвращает MatchNotFound")]
    public void ReplaceText_returns_not_found()
    {
        var part = NewPart("body");

        part.ReplaceText("absent", "y", Now).Should().BeOfType<ReplacePromptPartTextResult.MatchNotFound>();
    }

    [Fact(DisplayName = "ValidateModeRoles отклоняет неизвестный режим")]
    public void ValidateModeRoles_rejects_unknown_mode()
    {
        var act = () => PromptPart.ValidateModeRoles([new PromptPartModeRole("dream", PromptPartRoleNames.DefaultOn, 0)]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "ValidateModeRoles отклоняет дубликат режима")]
    public void ValidateModeRoles_rejects_duplicate_mode()
    {
        var act = () => PromptPart.ValidateModeRoles(
        [
            new PromptPartModeRole(PromptPartModeNames.Work, PromptPartRoleNames.DefaultOn, 0),
            new PromptPartModeRole(PromptPartModeNames.Work, PromptPartRoleNames.DefaultOff, 1),
        ]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "SetModeRoles заменяет роли и обновляет updated_at")]
    public void SetModeRoles_replaces_roles()
    {
        var part = NewPart("body", new PromptPartModeRole(PromptPartModeNames.Work, PromptPartRoleNames.DefaultOn, 0));

        part.SetModeRoles([new PromptPartModeRole(PromptPartModeNames.Interview, PromptPartRoleNames.Mandatory, 3)], Now.AddMinutes(5));

        part.ModeRoles.Should().ContainSingle()
            .Which.Mode.Should().Be(PromptPartModeNames.Interview);
        part.UpdatedAt.Should().Be(Now.AddMinutes(5));
    }
}
