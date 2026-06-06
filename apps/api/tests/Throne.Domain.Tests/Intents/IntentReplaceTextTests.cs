using FluentAssertions;
using Throne.Domain.Intents;
using Throne.Domain.TextVersions;

namespace Throne.Domain.Tests.Intents;

public class IntentReplaceTextTests
{
    private static readonly DateTimeOffset Created = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Edited = new(2026, 5, 1, 13, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "ReplaceText на одиночном вхождении возвращает Replaced и инкрементирует current_version")]
    public void Replace_single_occurrence_returns_replaced()
    {
        var intent = Intent.Create(IntentId.New(), "hello world", tagIds: null, Created);

        var result = intent.ReplaceText("world", "there", "ver-1", Edited, TextVersionAuthor.Agent);

        result.Should().BeOfType<ReplaceTextResult.Replaced>();
        var version = ((ReplaceTextResult.Replaced)result).Version;

        intent.State.Text.Should().Be("hello there");
        intent.State.CurrentVersion.Should().Be(2);
        intent.State.UpdatedAt.Should().Be(Edited);

        version.Id.Should().Be("ver-1");
        version.OwnerKind.Should().Be(TextVersionOwnerKind.Intent);
        version.OwnerId.Should().Be(intent.Id.Value);
        version.Version.Should().Be(2);
        version.Kind.Should().Be(TextVersionKind.Replace);
        version.Delta.Snapshot.Should().BeNull();
        version.Delta.OldText.Should().Be("world");
        version.Delta.NewText.Should().Be("there");
        version.Delta.AfterLine.Should().BeNull();
        version.Delta.InsertText.Should().BeNull();
        version.ChangedAt.Should().Be(Edited);
        version.ChangedBy.Should().Be(TextVersionAuthor.Agent);
    }

    [Fact(DisplayName = "ReplaceText без вхождений возвращает MatchNotFound и не меняет состояние Intent")]
    public void Replace_no_match_returns_match_not_found()
    {
        var intent = Intent.Create(IntentId.New(), "hello world", tagIds: null, Created);

        var result = intent.ReplaceText("xyz", "abc", "ver-1", Edited, TextVersionAuthor.Agent);

        result.Should().BeOfType<ReplaceTextResult.MatchNotFound>();
        ((ReplaceTextResult.MatchNotFound)result).QueryPreview.Should().Be("xyz");

        intent.State.Text.Should().Be("hello world");
        intent.State.CurrentVersion.Should().Be(1);
        intent.State.UpdatedAt.Should().Be(Created);
    }

    [Fact(DisplayName = "ReplaceText обрезает query_preview до 80 символов")]
    public void Replace_match_not_found_truncates_query_preview_to_80_chars()
    {
        var intent = Intent.Create(IntentId.New(), "abc", tagIds: null, Created);
        var longOldText = new string('x', 200);

        var result = intent.ReplaceText(longOldText, "y", "ver-1", Edited, TextVersionAuthor.Agent);

        var notFound = result.Should().BeOfType<ReplaceTextResult.MatchNotFound>().Subject;
        notFound.QueryPreview.Should().HaveLength(80);
        notFound.QueryPreview.Should().Be(new string('x', 80));
    }

    [Fact(DisplayName = "ReplaceText на нескольких вхождениях возвращает MatchAmbiguous с 1-indexed match_lines")]
    public void Replace_multiple_matches_returns_ambiguous()
    {
        var intent = Intent.Create(IntentId.New(), "foo\nbar\nfoo\nfoo", tagIds: null, Created);

        var result = intent.ReplaceText("foo", "qux", "ver-1", Edited, TextVersionAuthor.Agent);

        var ambiguous = result.Should().BeOfType<ReplaceTextResult.MatchAmbiguous>().Subject;
        ambiguous.MatchesCount.Should().Be(3);
        ambiguous.MatchLines.Should().Equal(1, 3, 4);

        intent.State.Text.Should().Be("foo\nbar\nfoo\nfoo");
        intent.State.CurrentVersion.Should().Be(1);
    }

    [Fact(DisplayName = "MatchAmbiguous обрезает match_lines до 5")]
    public void Replace_ambiguous_caps_match_lines_at_5()
    {
        var intent = Intent.Create(IntentId.New(), "x\nx\nx\nx\nx\nx\nx", tagIds: null, Created);

        var result = intent.ReplaceText("x", "y", "ver-1", Edited, TextVersionAuthor.Agent);

        var ambiguous = result.Should().BeOfType<ReplaceTextResult.MatchAmbiguous>().Subject;
        ambiguous.MatchesCount.Should().Be(7);
        ambiguous.MatchLines.Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact(DisplayName = "ReplaceText byte-exact: пробел значим")]
    public void Replace_is_byte_exact_for_whitespace()
    {
        var intent = Intent.Create(IntentId.New(), "hello world", tagIds: null, Created);

        var result = intent.ReplaceText("hello  world", "x", "ver-1", Edited, TextVersionAuthor.Agent);

        result.Should().BeOfType<ReplaceTextResult.MatchNotFound>();
    }

    [Fact(DisplayName = "ReplaceText допускает пустую newText (удаление фрагмента)")]
    public void Replace_allows_empty_new_text()
    {
        var intent = Intent.Create(IntentId.New(), "hello world", tagIds: null, Created);

        var result = intent.ReplaceText(" world", "", "ver-1", Edited, TextVersionAuthor.Agent);

        result.Should().BeOfType<ReplaceTextResult.Replaced>();
        intent.State.Text.Should().Be("hello");
        intent.State.CurrentVersion.Should().Be(2);
    }

    [Fact(DisplayName = "ReplaceText отвергает пустую oldText")]
    public void Replace_rejects_empty_old_text()
    {
        var intent = Intent.Create(IntentId.New(), "hello", tagIds: null, Created);

        var act = () => intent.ReplaceText("", "x", "ver-1", Edited, TextVersionAuthor.Agent);

        act.Should().Throw<ArgumentException>().WithParameterName("oldText");
    }
}
