using FluentAssertions;
using Throne.Domain.Intents;
using Throne.Domain.Tags;

namespace Throne.Domain.Tests.Intents;

public class IntentTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Create задаёт current_version = 1 и timestamps")]
    public void Create_starts_at_version_1()
    {
        var intent = Intent.Create(IntentId.New(), "user-1", "hello", tagIds: null, Now);

        intent.Status.Should().Be(IntentStatusNames.Draft);
        intent.CurrentVersion.Should().Be(1);
        intent.CreatedAt.Should().Be(Now);
        intent.UpdatedAt.Should().Be(Now);
        intent.TagIds.Should().BeEmpty();
    }

    [Fact(DisplayName = "Create нормализует tag_ids: dedup, выкидывает пустые")]
    public void Create_normalizes_tag_ids()
    {
        var a = TagId.New();
        var b = TagId.New();
        var intent = Intent.Create(
            IntentId.New(),
            "user-1",
            "x",
            [a, b, a, new TagId("")],
            Now);

        intent.TagIds.Should().Equal(a, b);
    }

    [Fact(DisplayName = "Create отвергает пустой text")]
    public void Create_rejects_empty_text()
    {
        var act = () => Intent.Create(IntentId.New(), "user-1", "", tagIds: null, Now);

        act.Should().Throw<ArgumentException>().WithParameterName("text");
    }

    [Fact(DisplayName = "Restore требует current_version >= 1")]
    public void Restore_rejects_zero_version()
    {
        var act = () => Intent.Restore(
            IntentId.New(),
            "user-1",
            "x",
            IntentStatusNames.Draft,
            currentVersion: 0,
            [],
            Now,
            Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact(DisplayName = "SetStatus меняет статус и updated_at")]
    public void SetStatus_updates_status_and_timestamp()
    {
        var intent = Intent.Create(IntentId.New(), "user-1", "hello", tagIds: null, Now);
        var later = Now.AddMinutes(5);

        var changed = intent.SetStatus(IntentStatusNames.Work, later);

        changed.Should().BeTrue();
        intent.Status.Should().Be(IntentStatusNames.Work);
        intent.UpdatedAt.Should().Be(later);
    }

    [Fact(DisplayName = "SetTagIds возвращает true только при реальной смене состава")]
    public void SetTagIds_changes_only_when_different()
    {
        var a = TagId.New();
        var b = TagId.New();
        var intent = Intent.Create(IntentId.New(), "user-1", "hello", [a], Now);
        var later = Now.AddMinutes(5);

        var unchanged = intent.SetTagIds([a], later);
        unchanged.Should().BeFalse();
        intent.UpdatedAt.Should().Be(Now);

        var changed = intent.SetTagIds([a, b], later);
        changed.Should().BeTrue();
        intent.TagIds.Should().Equal(a, b);
        intent.UpdatedAt.Should().Be(later);
    }

    [Fact(DisplayName = "Create без ownerUserId выбрасывает")]
    public void Create_rejects_empty_owner_user_id()
    {
        var act = () => Intent.Create(IntentId.New(), "", "text", tagIds: null, Now);
        act.Should().Throw<ArgumentException>().WithParameterName("ownerUserId");
    }

    [Fact(DisplayName = "OwnerUserId сохраняется")]
    public void Create_stores_owner_user_id()
    {
        var intent = Intent.Create(IntentId.New(), "alice", "text", tagIds: null, Now);
        intent.OwnerUserId.Should().Be("alice");
    }

    [Fact(DisplayName = "SetTagIds дедуплицирует и не бампит current_version")]
    public void SetTagIds_dedups_and_keeps_text_version()
    {
        var a = TagId.New();
        var intent = Intent.Create(IntentId.New(), "user-1", "hello", tagIds: null, Now);
        var versionBefore = intent.CurrentVersion;

        var changed = intent.SetTagIds([a, a], Now.AddSeconds(1));

        changed.Should().BeTrue();
        intent.TagIds.Should().Equal(a);
        intent.CurrentVersion.Should().Be(versionBefore);
    }
}
