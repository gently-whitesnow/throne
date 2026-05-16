using FluentAssertions;
using Throne.Domain.Instructions;

namespace Throne.Domain.Tests.Instructions;

public class InstructionPatchTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 9, 12, 0, 0, TimeSpan.Zero);

    private static InstructionPatch NewPatch(string status = InstructionPatchStatusNames.Proposed)
    {
        if (status == InstructionPatchStatusNames.Proposed)
        {
            return InstructionPatch.Create(
                id: "p-1",
                ownerUserId: "user-1",
                targetKind: InstructionKindNames.Work,
                patchText: "new instruction text",
                evidenceCardIds: ["card-1", "card-2"],
                rationale: "rationale",
                baseInstructionVersion: 5,
                now: Now);
        }
        return InstructionPatch.Restore(
            identity: new InstructionPatchIdentity("p-1", "user-1", InstructionKindNames.Work, 5, Now),
            state: new InstructionPatchState(
                Status: status,
                AppliedText: status == InstructionPatchStatusNames.Applied ? "new instruction text" : null,
                RejectComment: status == InstructionPatchStatusNames.Rejected
                    ? "rejected because too generic"
                    : null,
                AppliedInstructionVersion: status == InstructionPatchStatusNames.Applied ? 6 : null,
                UpdatedAt: Now,
                DecidedAt: Now),
            patchText: "new instruction text",
            evidenceCardIds: ["card-1"],
            rationale: "r");
    }

    [Fact(DisplayName = "Create стартует со status=proposed и сохраняет evidence/rationale/base_version")]
    public void Create_initial_state()
    {
        var patch = NewPatch();

        patch.State.Status.Should().Be(InstructionPatchStatusNames.Proposed);
        patch.Identity.OwnerUserId.Should().Be("user-1");
        patch.Identity.TargetKind.Should().Be(InstructionKindNames.Work);
        patch.Identity.BaseInstructionVersion.Should().Be(5);
        patch.PatchText.Should().Be("new instruction text");
        patch.State.AppliedText.Should().BeNull();
        patch.State.RejectComment.Should().BeNull();
        patch.State.AppliedInstructionVersion.Should().BeNull();
        patch.EvidenceCardIds.Should().Equal("card-1", "card-2");
        patch.Rationale.Should().Be("rationale");
        patch.State.DecidedAt.Should().BeNull();
    }

    [Fact(DisplayName = "Create отвергает unknown target_kind, base_version<0, evidence>лимита")]
    public void Create_validates_inputs()
    {
        var act1 = () => InstructionPatch.Create(
            "id", "user", "wat", "text", [], "r", 1, Now);
        act1.Should().Throw<ArgumentOutOfRangeException>();

        var act2 = () => InstructionPatch.Create(
            "id", "user", InstructionKindNames.Work, "text", [], "r", -1, Now);
        act2.Should().Throw<ArgumentOutOfRangeException>();

        var manyIds = Enumerable.Range(0, InstructionPatch.MaxEvidenceCardIds + 1)
            .Select(i => $"id-{i}").ToArray();
        var act3 = () => InstructionPatch.Create(
            "id", "user", InstructionKindNames.Work, "text", manyIds, "r", 1, Now);
        act3.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "MarkApplied verbatim переводит в Applied и сохраняет PatchText как AppliedText")]
    public void MarkApplied_verbatim()
    {
        var patch = NewPatch();

        var result = InstructionPatchTransitions.Apply(patch, editedText: null, appliedInstructionVersion: 6, Now.AddMinutes(1));

        result.Should().Be(InstructionPatchTransitions.ApplyResult.Ok);
        patch.State.Status.Should().Be(InstructionPatchStatusNames.Applied);
        patch.State.AppliedText.Should().Be("new instruction text");
        patch.State.AppliedInstructionVersion.Should().Be(6);
        patch.State.UpdatedAt.Should().Be(Now.AddMinutes(1));
        patch.State.DecidedAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact(DisplayName = "MarkApplied с другим текстом переводит в AppliedEdited и сохраняет правку")]
    public void MarkApplied_edited()
    {
        var patch = NewPatch();

        var result = InstructionPatchTransitions.Apply(patch, editedText: "operator-edited text", appliedInstructionVersion: 6, Now);

        result.Should().Be(InstructionPatchTransitions.ApplyResult.Ok);
        patch.State.Status.Should().Be(InstructionPatchStatusNames.AppliedEdited);
        patch.State.AppliedText.Should().Be("operator-edited text");
    }

    [Fact(DisplayName = "MarkApplied отвергает applied_version <= base_version")]
    public void MarkApplied_validates_version()
    {
        var patch = NewPatch();

        var result = InstructionPatchTransitions.Apply(patch, editedText: null, appliedInstructionVersion: 5, Now);

        result.Should().Be(InstructionPatchTransitions.ApplyResult.InvalidAppliedVersion);
        patch.State.Status.Should().Be(InstructionPatchStatusNames.Proposed);
    }

    [Fact(DisplayName = "MarkApplied на уже-Applied возвращает AlreadyDecided без мутации")]
    public void MarkApplied_idempotent()
    {
        var patch = NewPatch(InstructionPatchStatusNames.Applied);

        var result = InstructionPatchTransitions.Apply(patch, editedText: null, appliedInstructionVersion: 7, Now);

        result.Should().Be(InstructionPatchTransitions.ApplyResult.AlreadyDecided);
        patch.State.AppliedInstructionVersion.Should().Be(6);
    }

    [Fact(DisplayName = "MarkRejected требует comment ≥10 символов после trim")]
    public void MarkRejected_requires_long_comment()
    {
        var patch = NewPatch();

        InstructionPatchTransitions.Reject(patch, "short", Now)
            .Should().Be(InstructionPatchTransitions.RejectResult.CommentTooShort);
        InstructionPatchTransitions.Reject(patch, "   .   ", Now)
            .Should().Be(InstructionPatchTransitions.RejectResult.CommentTooShort);
        patch.State.Status.Should().Be(InstructionPatchStatusNames.Proposed);

        var ok = InstructionPatchTransitions.Reject(patch, "long enough explanation", Now.AddMinutes(1));
        ok.Should().Be(InstructionPatchTransitions.RejectResult.Ok);
        patch.State.Status.Should().Be(InstructionPatchStatusNames.Rejected);
        patch.State.RejectComment.Should().Be("long enough explanation");
        patch.State.DecidedAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact(DisplayName = "MarkRejected на уже-rejected возвращает AlreadyDecided")]
    public void MarkRejected_idempotent()
    {
        var patch = NewPatch(InstructionPatchStatusNames.Rejected);

        InstructionPatchTransitions.Reject(patch, "another long comment text", Now)
            .Should().Be(InstructionPatchTransitions.RejectResult.AlreadyDecided);
    }
}
