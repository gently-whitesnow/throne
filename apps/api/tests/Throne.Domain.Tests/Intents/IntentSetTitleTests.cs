using FluentAssertions;
using Throne.Domain.Intents;

namespace Throne.Domain.Tests.Intents;

public class IntentSetTitleTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Create без title оставляет Title = null")]
    public void Create_without_title_is_null()
    {
        var intent = Intent.Create(IntentId.New(), "text", tagIds: null, Now);

        intent.State.Title.Should().BeNull();
    }

    [Fact(DisplayName = "Create с title тримит и сохраняет его")]
    public void Create_with_title_trims()
    {
        var intent = Intent.Create(IntentId.New(), "text", tagIds: null, Now, title: "  My title  ");

        intent.State.Title.Should().Be("My title");
    }

    [Fact(DisplayName = "Create с пустым/пробельным title даёт Title = null")]
    public void Create_with_blank_title_is_null()
    {
        var intent = Intent.Create(IntentId.New(), "text", tagIds: null, Now, title: "   ");

        intent.State.Title.Should().BeNull();
    }

    [Fact(DisplayName = "SetTitle меняет title, бьёт UpdatedAt, не трогает current_version")]
    public void SetTitle_is_metadata()
    {
        var intent = Intent.Create(IntentId.New(), "text", tagIds: null, Now);
        var later = Now.AddMinutes(3);

        var changed = intent.SetTitle("A title", later);

        changed.Should().BeTrue();
        intent.State.Title.Should().Be("A title");
        intent.State.UpdatedAt.Should().Be(later);
        intent.State.CurrentVersion.Should().Be(1);
    }

    [Fact(DisplayName = "SetTitle идемпотентен: тот же нормализованный title → false, UpdatedAt не двигается")]
    public void SetTitle_idempotent()
    {
        var intent = Intent.Create(IntentId.New(), "text", tagIds: null, Now, title: "Title");
        var later = Now.AddMinutes(3);

        var changed = intent.SetTitle("  Title  ", later);

        changed.Should().BeFalse();
        intent.State.Title.Should().Be("Title");
        intent.State.UpdatedAt.Should().Be(Now);
    }

    [Fact(DisplayName = "SetTitle с пустым значением очищает title")]
    public void SetTitle_blank_clears()
    {
        var intent = Intent.Create(IntentId.New(), "text", tagIds: null, Now, title: "Title");
        var later = Now.AddMinutes(3);

        var changed = intent.SetTitle("", later);

        changed.Should().BeTrue();
        intent.State.Title.Should().BeNull();
        intent.State.UpdatedAt.Should().Be(later);
    }
}
