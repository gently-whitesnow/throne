using FluentAssertions;
using Throne.Domain.PromptParts;
using Throne.Domain.TextVersions;

namespace Throne.Domain.Tests.PromptParts;

public class PromptPartTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    private static PromptPart NewPart(string text, params PromptPartModeRole[] roles) => PromptPart.Create(
        PromptPartId.New(), PromptPartScopeNames.User, "architecture", text, "desc", roles, Now);

    [Fact(DisplayName = "Create задаёт версию 1 и сохраняет роли")]
    public void Create_initializes_version_and_roles()
    {
        var part = NewPart("body", new PromptPartModeRole(PromptPartModeNames.Work, PromptPartRoleNames.DefaultOn, 2));

        part.CurrentVersion.Should().Be(1);
        part.Scope.Should().Be(PromptPartScopeNames.User);
        part.ModeRoles.Should().ContainSingle()
            .Which.Should().Be(new PromptPartModeRole(PromptPartModeNames.Work, PromptPartRoleNames.DefaultOn, 2));
    }

    [Fact(DisplayName = "ReplaceText заменяет уникальную подстроку, бампит версию и возвращает TextVersion")]
    public void ReplaceText_replaces_unique_substring()
    {
        var part = NewPart("alpha beta");

        var result = part.ReplaceText("beta", "gamma", "ver-2", Now.AddMinutes(1), TextVersionAuthor.User);

        var replaced = result.Should().BeOfType<ReplacePromptPartTextResult.Replaced>().Subject;
        part.Text.Should().Be("alpha gamma");
        part.CurrentVersion.Should().Be(2);
        part.UpdatedAt.Should().Be(Now.AddMinutes(1));
        replaced.Version.Version.Should().Be(2);
        replaced.Version.OwnerKind.Should().Be(TextVersionOwnerKind.PromptPart);
        replaced.Version.OwnerId.Should().Be(part.Id.Value);
    }

    [Fact(DisplayName = "ReplaceText на пустом тексте с old_text=\"\" заполняет инициально")]
    public void ReplaceText_initial_fill_for_empty_part()
    {
        var part = PromptPart.Create(PromptPartId.New(), PromptPartScopeNames.User, "work", string.Empty, null, [], Now);

        var result = part.ReplaceText(string.Empty, "первый текст", "ver-2", Now.AddMinutes(1), TextVersionAuthor.User);

        result.Should().BeOfType<ReplacePromptPartTextResult.Replaced>();
        part.Text.Should().Be("первый текст");
        part.CurrentVersion.Should().Be(2);
    }

    [Fact(DisplayName = "ReplaceText с old_text=\"\" на непустом тексте бросает ArgumentException")]
    public void ReplaceText_empty_old_on_nonempty_text_rejected()
    {
        var part = NewPart("текущий текст");

        var act = () => part.ReplaceText(string.Empty, "новый", "ver-2", Now, TextVersionAuthor.User);

        act.Should().Throw<ArgumentException>().WithMessage("*initial fill*");
    }

    [Fact(DisplayName = "ReplaceText на неоднозначной подстроке возвращает MatchAmbiguous без мутации")]
    public void ReplaceText_returns_ambiguous()
    {
        var part = NewPart("x x");

        var result = part.ReplaceText("x", "y", "ver-2", Now, TextVersionAuthor.User);

        result.Should().BeOfType<ReplacePromptPartTextResult.MatchAmbiguous>();
        part.Text.Should().Be("x x");
        part.CurrentVersion.Should().Be(1);
    }

    [Fact(DisplayName = "ReplaceText на отсутствующей подстроке возвращает MatchNotFound")]
    public void ReplaceText_returns_not_found()
    {
        var part = NewPart("body");

        part.ReplaceText("absent", "y", "ver-2", Now, TextVersionAuthor.User)
            .Should().BeOfType<ReplacePromptPartTextResult.MatchNotFound>();
    }

    [Fact(DisplayName = "ValidateModeRoles отклоняет неизвестный режим")]
    public void ValidateModeRoles_rejects_unknown_mode()
    {
        var act = () => PromptPart.ValidateModeRoles([new PromptPartModeRole("bogus_mode", PromptPartRoleNames.DefaultOn, 0)]);

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

    [Fact(DisplayName = "Restore отбрасывает роль с выпиленным режимом, не падая")]
    public void Restore_drops_retired_mode_role()
    {
        var part = PromptPart.Restore(
            PromptPartId.New(), PromptPartScopeNames.System, "common", "body", "desc", 3,
            [
                new PromptPartModeRole(PromptPartModeNames.Work, PromptPartRoleNames.DefaultOn, 0),
                new PromptPartModeRole("schema_map", PromptPartRoleNames.Mandatory, 1),
            ],
            Now, Now);

        part.ModeRoles.Should().ContainSingle()
            .Which.Mode.Should().Be(PromptPartModeNames.Work);
    }

    [Fact(DisplayName = "Restore допускает пустые роли после отбрасывания всех выпиленных")]
    public void Restore_allows_empty_after_dropping_all_retired()
    {
        var part = PromptPart.Restore(
            PromptPartId.New(), PromptPartScopeNames.System, "schema_map", "body", "desc", 1,
            [new PromptPartModeRole("schema_map", PromptPartRoleNames.Mandatory, 0)],
            Now, Now);

        part.ModeRoles.Should().BeEmpty();
    }
}
