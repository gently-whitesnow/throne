using FluentAssertions;
using Throne.Domain.Intents;
using Throne.Domain.TextVersions;

namespace Throne.Domain.Tests.Intents;

public class IntentInsertAfterLineTests
{
    private static readonly DateTimeOffset Created = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Edited = new(2026, 5, 1, 13, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Insert at after_line=0 prepends and increments current_version")]
    public void Insert_at_zero_prepends()
    {
        var intent = Intent.Create(IntentId.New(), "alpha\nbeta", tagIds: null, Created);

        var result = intent.InsertTextAfterLine(afterLine: 0, insertText: "head\n", "ver-1", Edited, TextVersionAuthor.Agent);

        var inserted = result.Should().BeOfType<InsertTextResult.Inserted>().Subject;
        intent.State.Text.Should().Be("head\nalpha\nbeta");
        intent.State.CurrentVersion.Should().Be(2);
        intent.State.UpdatedAt.Should().Be(Edited);
        inserted.Version.Kind.Should().Be(TextVersionKind.Insert);
        inserted.Version.Delta.AfterLine.Should().Be(0);
        inserted.Version.Delta.InsertText.Should().Be("head\n");
        inserted.Version.Version.Should().Be(2);
    }

    [Fact(DisplayName = "Insert at after_line=N inserts after the N-th line")]
    public void Insert_after_middle_line()
    {
        var intent = Intent.Create(IntentId.New(), "a\nb\nc", tagIds: null, Created);

        var result = intent.InsertTextAfterLine(afterLine: 1, insertText: "X\n", "ver-1", Edited, TextVersionAuthor.Agent);

        result.Should().BeOfType<InsertTextResult.Inserted>();
        intent.State.Text.Should().Be("a\nX\nb\nc");
    }

    [Fact(DisplayName = "Insert at after_line=total_lines appends to end")]
    public void Insert_at_total_lines_appends()
    {
        var intent = Intent.Create(IntentId.New(), "a\nb", tagIds: null, Created);

        var result = intent.InsertTextAfterLine(afterLine: 2, insertText: "\nc", "ver-1", Edited, TextVersionAuthor.Agent);

        result.Should().BeOfType<InsertTextResult.Inserted>();
        intent.State.Text.Should().Be("a\nb\nc");
    }

    [Fact(DisplayName = "Insert with after_line beyond total_lines returns LineOutOfRange and does not mutate")]
    public void Insert_out_of_range_returns_line_out_of_range()
    {
        var intent = Intent.Create(IntentId.New(), "a\nb", tagIds: null, Created);

        var result = intent.InsertTextAfterLine(afterLine: 5, insertText: "x", "ver-1", Edited, TextVersionAuthor.Agent);

        var oor = result.Should().BeOfType<InsertTextResult.LineOutOfRange>().Subject;
        oor.TotalLines.Should().Be(2);
        oor.RequestedAfterLine.Should().Be(5);

        intent.State.Text.Should().Be("a\nb");
        intent.State.CurrentVersion.Should().Be(1);
    }

    [Fact(DisplayName = "Insert with negative after_line returns LineOutOfRange")]
    public void Insert_negative_returns_line_out_of_range()
    {
        var intent = Intent.Create(IntentId.New(), "a", tagIds: null, Created);

        var result = intent.InsertTextAfterLine(afterLine: -1, insertText: "x", "ver-1", Edited, TextVersionAuthor.Agent);

        result.Should().BeOfType<InsertTextResult.LineOutOfRange>();
    }

    [Fact(DisplayName = "Multi-line insert keeps content as-is (no auto newline)")]
    public void Multiline_insert_no_auto_newline()
    {
        var intent = Intent.Create(IntentId.New(), "a\nb", tagIds: null, Created);

        intent.InsertTextAfterLine(afterLine: 1, insertText: "X\nY", "ver-1", Edited, TextVersionAuthor.Agent);

        intent.State.Text.Should().Be("a\nX\nYb");
    }
}
