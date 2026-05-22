using FluentAssertions;
using Throne.Domain.TextVersions;

namespace Throne.Domain.Tests.TextVersions;

public class TextVersionReplayTests
{
    private static readonly DateTimeOffset At = new(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
    private const string Owner = "instr-1";

    [Fact(DisplayName = "Пустая история → пустая строка")]
    public void Empty_history_returns_empty()
    {
        TextVersionReplay.ReplayTo(Array.Empty<TextVersion>(), targetVersion: 5).Should().BeEmpty();
    }

    [Fact(DisplayName = "targetVersion=0 → пустая строка даже при наличии snapshot")]
    public void Target_zero_returns_empty()
    {
        var v1 = Snapshot(version: 1, text: "hello");
        TextVersionReplay.ReplayTo(new[] { v1 }, targetVersion: 0).Should().BeEmpty();
    }

    [Fact(DisplayName = "Replay до версии snapshot возвращает snapshot текст")]
    public void Replay_to_snapshot_version()
    {
        var v1 = Snapshot(version: 1, text: "hello world");
        TextVersionReplay.ReplayTo(new[] { v1 }, targetVersion: 1).Should().Be("hello world");
    }

    [Fact(DisplayName = "Replay цепочки create→replace воспроизводит промежуточные состояния")]
    public void Replay_create_then_replace()
    {
        var v1 = Snapshot(1, "hello world");
        var v2 = Replace(2, oldText: "world", newText: "there");
        var v3 = Replace(3, oldText: "hello", newText: "hi");
        var versions = new[] { v1, v2, v3 };

        TextVersionReplay.ReplayTo(versions, 1).Should().Be("hello world");
        TextVersionReplay.ReplayTo(versions, 2).Should().Be("hello there");
        TextVersionReplay.ReplayTo(versions, 3).Should().Be("hi there");
    }

    [Fact(DisplayName = "Replay цепочки create→insert корректно расставляет вставки по афтер-лайну")]
    public void Replay_create_then_insert()
    {
        var v1 = Snapshot(1, "line a\nline b\n");
        var v2 = Insert(2, afterLine: 1, insertText: "inserted\n");

        TextVersionReplay.ReplayTo(new[] { v1, v2 }, 2).Should().Be("line a\ninserted\nline b\n");
    }

    [Fact(DisplayName = "Insert с afterLine=0 кладёт текст в начало")]
    public void Insert_at_zero_prepends()
    {
        var v1 = Snapshot(1, "tail\n");
        var v2 = Insert(2, afterLine: 0, insertText: "head\n");

        TextVersionReplay.ReplayTo(new[] { v1, v2 }, 2).Should().Be("head\ntail\n");
    }

    [Fact(DisplayName = "Версии выше target игнорируются")]
    public void Versions_above_target_ignored()
    {
        var v1 = Snapshot(1, "a");
        var v2 = Replace(2, "a", "b");
        var v3 = Replace(3, "b", "c");

        TextVersionReplay.ReplayTo(new[] { v3, v2, v1 }, targetVersion: 2).Should().Be("b");
    }

    [Fact(DisplayName = "Версии в произвольном порядке сортируются перед применением")]
    public void Versions_unsorted_input_sorted_internally()
    {
        var v1 = Snapshot(1, "a");
        var v2 = Replace(2, "a", "ab");
        var v3 = Replace(3, "ab", "abc");

        TextVersionReplay.ReplayTo(new[] { v3, v1, v2 }, targetVersion: 3).Should().Be("abc");
    }

    [Fact(DisplayName = "Replace с отсутствующим old_text не падает, оставляет текст как есть")]
    public void Replace_unmatched_is_noop()
    {
        var v1 = Snapshot(1, "hello");
        var v2 = Replace(2, oldText: "world", newText: "there");

        TextVersionReplay.ReplayTo(new[] { v1, v2 }, 2).Should().Be("hello");
    }

    [Fact(DisplayName = "Replace заменяет только первое вхождение (детерминированно)")]
    public void Replace_first_occurrence_only()
    {
        var v1 = Snapshot(1, "aa bb aa");
        var v2 = Replace(2, "aa", "XX");

        TextVersionReplay.ReplayTo(new[] { v1, v2 }, 2).Should().Be("XX bb aa");
    }

    [Fact(DisplayName = "Insert с afterLine больше количества строк добавляет в конец")]
    public void Insert_past_end_appends()
    {
        var v1 = Snapshot(1, "only line");
        var v2 = Insert(2, afterLine: 99, insertText: " extra");

        TextVersionReplay.ReplayTo(new[] { v1, v2 }, 2).Should().Be("only line extra");
    }

    private static TextVersion Snapshot(int version, string text) => new(
        Id: $"v{version}",
        OwnerKind: TextVersionOwnerKind.Instruction,
        OwnerId: Owner,
        Version: version,
        Kind: TextVersionKind.Create,
        Delta: new TextVersionDelta(Snapshot: text, OldText: null, NewText: null, AfterLine: null, InsertText: null),
        ChangedAt: At,
        ChangedBy: TextVersionAuthor.User);

    private static TextVersion Replace(int version, string oldText, string newText) => new(
        Id: $"v{version}",
        OwnerKind: TextVersionOwnerKind.Instruction,
        OwnerId: Owner,
        Version: version,
        Kind: TextVersionKind.Replace,
        Delta: new TextVersionDelta(Snapshot: null, OldText: oldText, NewText: newText, AfterLine: null, InsertText: null),
        ChangedAt: At,
        ChangedBy: TextVersionAuthor.User);

    private static TextVersion Insert(int version, int afterLine, string insertText) => new(
        Id: $"v{version}",
        OwnerKind: TextVersionOwnerKind.Instruction,
        OwnerId: Owner,
        Version: version,
        Kind: TextVersionKind.Insert,
        Delta: new TextVersionDelta(Snapshot: null, OldText: null, NewText: null, AfterLine: afterLine, InsertText: insertText),
        ChangedAt: At,
        ChangedBy: TextVersionAuthor.User);
}
