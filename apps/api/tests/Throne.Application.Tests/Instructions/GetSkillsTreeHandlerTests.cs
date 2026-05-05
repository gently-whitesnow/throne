using FluentAssertions;
using NSubstitute;
using Throne.Application.Instructions;
using Throne.Application.Ports;
using Throne.Domain.Instructions;

namespace Throne.Application.Tests.Instructions;

public class GetSkillsTreeHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "GetSkillsTree собирает все 4 skill-а с правильным составом bundle")]
    public async Task Returns_all_skills_with_correct_bundles()
    {
        var repo = Substitute.For<IInstructionRepository>();
        repo.GetUserInstructionsByKindsAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var handler = new GetSkillsTreeHandler(SkillManifestFixtures.Provider(), repo, new TestCurrentUserAccessor());

        var tree = await handler.HandleAsync(new GetSkillsTreeQuery(), CancellationToken.None);

        tree.Skills.Select(s => s.Name).Should().Equal(
            "tinterview", "twork", "tfix", "tdream");

        var work = tree.Skills.Single(s => s.Name == "twork");
        work.Bundle.Mode.Should().Be(InstructionBundleModeNames.Work);
        work.Bundle.Includes.Should().HaveCount(4);
        work.Bundle.Includes.Select(i => (i.Scope, i.Kind)).Should().Equal(
            (InstructionScopeNames.System, InstructionKindNames.Common),
            (InstructionScopeNames.System, InstructionKindNames.Work),
            (InstructionScopeNames.User, InstructionKindNames.Common),
            (InstructionScopeNames.User, InstructionKindNames.Work));
    }

    [Fact(DisplayName = "GetSkillsTree помечает system узлы как read-only с синтетическим id и текстом из манифеста")]
    public async Task System_nodes_are_read_only_with_synthetic_id()
    {
        var repo = Substitute.For<IInstructionRepository>();
        repo.GetUserInstructionsByKindsAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var handler = new GetSkillsTreeHandler(SkillManifestFixtures.Provider(), repo, new TestCurrentUserAccessor());

        var tree = await handler.HandleAsync(new GetSkillsTreeQuery(), CancellationToken.None);

        var work = tree.Skills.Single(s => s.Name == "twork");
        var sysWork = work.Bundle.Includes.Single(i =>
            i.Scope == InstructionScopeNames.System && i.Kind == InstructionKindNames.Work);
        sysWork.Editable.Should().BeFalse();
        sysWork.Present.Should().BeTrue();
        sysWork.InstructionId.Should().Be("system:work");
        sysWork.CurrentVersion.Should().Be(1);
        sysWork.Text.Should().Be("system text for work");
    }

    [Fact(DisplayName = "GetSkillsTree подтягивает существующие user инструкции по соответствующему kind")]
    public async Task User_nodes_carry_existing_mongo_records()
    {
        var repo = Substitute.For<IInstructionRepository>();
        var userCommon = Instruction.Create(
            InstructionId.New(), InstructionScopeNames.User, "user-1", InstructionKindNames.Common, "user common", Now);
        var userWork = Instruction.Create(
            InstructionId.New(), InstructionScopeNames.User, "user-1", InstructionKindNames.Work, "user work", Now);
        repo.GetUserInstructionsByKindsAsync("user-1", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([userCommon, userWork]);
        var handler = new GetSkillsTreeHandler(SkillManifestFixtures.Provider(), repo, new TestCurrentUserAccessor());

        var tree = await handler.HandleAsync(new GetSkillsTreeQuery(), CancellationToken.None);

        var work = tree.Skills.Single(s => s.Name == "twork");
        var userWorkEntry = work.Bundle.Includes.Single(i =>
            i.Scope == InstructionScopeNames.User && i.Kind == InstructionKindNames.Work);
        userWorkEntry.Editable.Should().BeTrue();
        userWorkEntry.Present.Should().BeTrue();
        userWorkEntry.InstructionId.Should().Be(userWork.Id.Value);
        userWorkEntry.Text.Should().Be("user work");
    }

    [Fact(DisplayName = "GetSkillsTree помечает отсутствующие user инструкции present=false с пустым текстом")]
    public async Task Missing_user_instruction_marked_absent()
    {
        var repo = Substitute.For<IInstructionRepository>();
        repo.GetUserInstructionsByKindsAsync("user-1", Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>())
            .Returns([]);
        var handler = new GetSkillsTreeHandler(SkillManifestFixtures.Provider(), repo, new TestCurrentUserAccessor());

        var tree = await handler.HandleAsync(new GetSkillsTreeQuery(), CancellationToken.None);

        var dream = tree.Skills.Single(s => s.Name == "tdream");
        var userDream = dream.Bundle.Includes.Single(i =>
            i.Scope == InstructionScopeNames.User && i.Kind == InstructionKindNames.Dream);
        userDream.Present.Should().BeFalse();
        userDream.InstructionId.Should().BeNull();
        userDream.CurrentVersion.Should().Be(0);
        userDream.Text.Should().BeEmpty();
        userDream.Editable.Should().BeTrue();
    }
}
