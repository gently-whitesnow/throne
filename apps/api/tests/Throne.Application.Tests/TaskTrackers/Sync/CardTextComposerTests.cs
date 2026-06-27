using FluentAssertions;
using Throne.Application.TaskTrackers.Sync;
using Throne.Domain.TaskTrackers;

namespace Throne.Application.Tests.TaskTrackers.Sync;

public class CardTextComposerTests
{
    [Fact(DisplayName = "Compose склеивает заголовок и описание через пустую строку")]
    public void Compose_joins_title_and_description()
    {
        CardTextComposer.Compose("Title", "Body line").Should().Be("Title\n\nBody line");
    }

    [Theory(DisplayName = "Compose с пустым/пробельным описанием оставляет только заголовок")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t ")]
    public void Compose_drops_blank_description(string? description)
    {
        CardTextComposer.Compose("Title", description).Should().Be("Title");
    }

    [Theory(DisplayName = "Compose с пустым заголовком подставляет плейсхолдер")]
    [InlineData("")]
    [InlineData("   ")]
    public void Compose_uses_placeholder_for_blank_title(string title)
    {
        CardTextComposer.Compose(title, null).Should().Be("(без названия)");
    }

    [Fact(DisplayName = "Compose тримит хвост заголовка и края описания")]
    public void Compose_trims()
    {
        CardTextComposer.Compose("Title  ", "  Body  ").Should().Be("Title\n\nBody");
    }

    [Fact(DisplayName = "Decompose: одна строка → заголовок, описание null")]
    public void Decompose_single_line()
    {
        var (title, description) = CardTextComposer.Decompose("Just a title");

        title.Should().Be("Just a title");
        description.Should().BeNull();
    }

    [Fact(DisplayName = "Decompose: первая строка — заголовок, остаток — описание")]
    public void Decompose_splits_first_line()
    {
        var (title, description) = CardTextComposer.Decompose("Title\n\nBody line\nmore");

        title.Should().Be("Title");
        description.Should().Be("Body line\nmore");
    }

    [Fact(DisplayName = "Decompose: пустой остаток даёт описание null")]
    public void Decompose_blank_rest_is_null()
    {
        var (title, description) = CardTextComposer.Decompose("Title\n\n");

        title.Should().Be("Title");
        description.Should().BeNull();
    }

    [Fact(DisplayName = "Matches: true когда intent-текст == Compose(snapshot)")]
    public void Matches_true_when_equal()
    {
        var snapshot = new CardSyncLinkSnapshot("Title", "Body", "col", "Column");

        CardTextComposer.Matches(snapshot, "Title\n\nBody").Should().BeTrue();
    }

    [Fact(DisplayName = "Matches: false когда intent-текст расходится со снапшотом")]
    public void Matches_false_when_diverged()
    {
        var snapshot = new CardSyncLinkSnapshot("Title", "Body", null, null);

        CardTextComposer.Matches(snapshot, "Title\n\nChanged").Should().BeFalse();
    }

    [Fact(DisplayName = "Matches: null-текст не падает и не совпадает с непустым снапшотом")]
    public void Matches_handles_null_text()
    {
        var snapshot = new CardSyncLinkSnapshot("Title", null, null, null);

        CardTextComposer.Matches(snapshot, null!).Should().BeFalse();
    }

    [Theory(DisplayName = "Round-trip: Decompose(Compose(t,d)) сохраняет канонические t и d")]
    [InlineData("Title", "Description body")]
    [InlineData("Title", "multi\nline\nbody")]
    [InlineData("Only title", null)]
    public void Round_trip_preserves_canonical_input(string title, string? description)
    {
        var text = CardTextComposer.Compose(title, description);

        var (rtTitle, rtDescription) = CardTextComposer.Decompose(text);

        rtTitle.Should().Be(title);
        rtDescription.Should().Be(description);
    }
}
