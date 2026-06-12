using FluentAssertions;
using Throne.Domain.PromptParts;

namespace Throne.Domain.Tests.PromptParts;

public class PromptPartPatchTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 12, 12, 0, 0, TimeSpan.Zero);

    private static PromptPartPatch NewPatch(string status = PromptPartPatchStatusNames.Proposed)
    {
        if (status == PromptPartPatchStatusNames.Proposed)
        {
            return PromptPartPatch.Create(
                id: "p-1",
                targetScope: PromptPartScopeNames.User,
                targetKey: "work",
                patchText: "new part text",
                evidenceCardIds: ["card-1", "card-2"],
                rationale: "rationale",
                baseVersion: 5,
                now: Now);
        }
        return PromptPartPatch.Restore(
            identity: new PromptPartPatchIdentity("p-1", PromptPartScopeNames.User, "work", 5, Now),
            state: new PromptPartPatchState(
                Status: status,
                AppliedText: status == PromptPartPatchStatusNames.Applied ? "new part text" : null,
                RejectComment: status == PromptPartPatchStatusNames.Rejected
                    ? "rejected because too generic"
                    : null,
                AppliedVersion: status == PromptPartPatchStatusNames.Applied ? 6 : null,
                UpdatedAt: Now,
                DecidedAt: Now),
            patchText: "new part text",
            evidenceCardIds: ["card-1"],
            rationale: "r");
    }

    [Fact(DisplayName = "Create стартует со status=proposed и сохраняет target/evidence/base_version")]
    public void Create_initial_state()
    {
        var patch = NewPatch();

        patch.State.Status.Should().Be(PromptPartPatchStatusNames.Proposed);
        patch.Identity.TargetScope.Should().Be(PromptPartScopeNames.User);
        patch.Identity.TargetKey.Should().Be("work");
        patch.Identity.BaseVersion.Should().Be(5);
        patch.PatchText.Should().Be("new part text");
        patch.State.AppliedText.Should().BeNull();
        patch.State.RejectComment.Should().BeNull();
        patch.State.AppliedVersion.Should().BeNull();
        patch.EvidenceCardIds.Should().Equal("card-1", "card-2");
        patch.Rationale.Should().Be("rationale");
        patch.State.DecidedAt.Should().BeNull();
    }

    [Fact(DisplayName = "Create отвергает unknown target_scope, base_version<0, evidence>лимита")]
    public void Create_validates_inputs()
    {
        var act1 = () => PromptPartPatch.Create(
            "id", "wat", "work", "text", [], "r", 1, Now);
        act1.Should().Throw<ArgumentOutOfRangeException>();

        var act2 = () => PromptPartPatch.Create(
            "id", PromptPartScopeNames.User, "work", "text", [], "r", -1, Now);
        act2.Should().Throw<ArgumentOutOfRangeException>();

        var manyIds = Enumerable.Range(0, PromptPartPatch.MaxEvidenceCardIds + 1)
            .Select(i => $"id-{i}").ToArray();
        var act3 = () => PromptPartPatch.Create(
            "id", PromptPartScopeNames.User, "work", "text", manyIds, "r", 1, Now);
        act3.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "Apply verbatim переводит в Applied и сохраняет PatchText как AppliedText")]
    public void Apply_verbatim()
    {
        var patch = NewPatch();

        var result = patch.Apply(editedText: null, appliedVersion: 6, Now.AddMinutes(1));

        result.Should().Be(PromptPartPatch.ApplyResult.Ok);
        patch.State.Status.Should().Be(PromptPartPatchStatusNames.Applied);
        patch.State.AppliedText.Should().Be("new part text");
        patch.State.AppliedVersion.Should().Be(6);
        patch.State.UpdatedAt.Should().Be(Now.AddMinutes(1));
        patch.State.DecidedAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact(DisplayName = "Apply с другим текстом переводит в AppliedEdited и сохраняет правку")]
    public void Apply_edited()
    {
        var patch = NewPatch();

        var result = patch.Apply(editedText: "operator-edited text", appliedVersion: 6, Now);

        result.Should().Be(PromptPartPatch.ApplyResult.Ok);
        patch.State.Status.Should().Be(PromptPartPatchStatusNames.AppliedEdited);
        patch.State.AppliedText.Should().Be("operator-edited text");
    }

    [Fact(DisplayName = "Apply отвергает applied_version <= base_version")]
    public void Apply_validates_version()
    {
        var patch = NewPatch();

        var result = patch.Apply(editedText: null, appliedVersion: 5, Now);

        result.Should().Be(PromptPartPatch.ApplyResult.InvalidAppliedVersion);
        patch.State.Status.Should().Be(PromptPartPatchStatusNames.Proposed);
    }

    [Fact(DisplayName = "Apply на уже-Applied возвращает AlreadyDecided без мутации")]
    public void Apply_idempotent()
    {
        var patch = NewPatch(PromptPartPatchStatusNames.Applied);

        var result = patch.Apply(editedText: null, appliedVersion: 7, Now);

        result.Should().Be(PromptPartPatch.ApplyResult.AlreadyDecided);
        patch.State.AppliedVersion.Should().Be(6);
    }

    [Fact(DisplayName = "Reject требует comment ≥10 символов после trim")]
    public void Reject_requires_long_comment()
    {
        var patch = NewPatch();

        patch.Reject("short", Now)
            .Should().Be(PromptPartPatch.RejectResult.CommentTooShort);
        patch.Reject("   .   ", Now)
            .Should().Be(PromptPartPatch.RejectResult.CommentTooShort);
        patch.State.Status.Should().Be(PromptPartPatchStatusNames.Proposed);

        var ok = patch.Reject("long enough explanation", Now.AddMinutes(1));
        ok.Should().Be(PromptPartPatch.RejectResult.Ok);
        patch.State.Status.Should().Be(PromptPartPatchStatusNames.Rejected);
        patch.State.RejectComment.Should().Be("long enough explanation");
        patch.State.DecidedAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact(DisplayName = "Reject на уже-rejected возвращает AlreadyDecided")]
    public void Reject_idempotent()
    {
        var patch = NewPatch(PromptPartPatchStatusNames.Rejected);

        patch.Reject("another long comment text", Now)
            .Should().Be(PromptPartPatch.RejectResult.AlreadyDecided);
    }
}
