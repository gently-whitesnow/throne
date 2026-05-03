using FluentAssertions;
using Throne.Application.DreamRuns;
using Throne.Application.Ports;

namespace Throne.Application.Tests.DreamRuns;

public class ContextTokenCounterTests
{
    private static readonly DateTimeOffset At = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Пустое окно → 0 токенов")]
    public void Empty_window_returns_zero()
    {
        var counter = new ContextTokenCounter(new LengthTokenizer());
        var window = new IntentWindow([]);

        var result = counter.Count(window);

        result.TotalTokens.Should().Be(0);
        result.PerIntent.Should().BeEmpty();
    }

    [Fact(DisplayName = "Финальный Intent.text дедуплицируется против последнего snapshot")]
    public void Final_text_dedupes_against_last_version()
    {
        var counter = new ContextTokenCounter(new LengthTokenizer());
        var intent = new IntentInWindow(
            "intent-1",
            CurrentText: "snapshot-2",
            TextVersions: [
                new IntentTextVersionSnapshot(1, "create", Snapshot: "snapshot-1", null, null, null),
                new IntentTextVersionSnapshot(2, "replace", Snapshot: null, OldText: null, NewText: "snapshot-2", InsertText: null),
            ],
            QaList: [],
            ReviewList: [],
            UpdatedAt: At);
        var window = new IntentWindow([intent]);

        var result = counter.Count(window);

        // parts: "snapshot-1" + "\n" + "snapshot-2"  → 21 chars (current text NOT re-added)
        result.TotalTokens.Should().Be("snapshot-1\nsnapshot-2".Length);
        result.PerIntent.Should().HaveCount(1);
        result.PerIntent[0].IntentId.Should().Be("intent-1");
    }

    [Fact(DisplayName = "Финальный текст добавляется, если расходится с последним snapshot")]
    public void Final_text_added_when_drifted_from_last_version()
    {
        var counter = new ContextTokenCounter(new LengthTokenizer());
        var intent = new IntentInWindow(
            "intent-1",
            CurrentText: "drifted",
            TextVersions: [
                new IntentTextVersionSnapshot(1, "create", Snapshot: "old-text", null, null, null),
            ],
            QaList: [],
            ReviewList: [],
            UpdatedAt: At);
        var window = new IntentWindow([intent]);

        var result = counter.Count(window);

        result.TotalTokens.Should().Be("old-text\ndrifted".Length);
    }

    [Fact(DisplayName = "QA и review сериализуются и считаются")]
    public void Qa_and_reviews_are_counted()
    {
        var counter = new ContextTokenCounter(new LengthTokenizer());
        var intent = new IntentInWindow(
            "intent-1",
            CurrentText: "current",
            TextVersions: [],
            QaList: [
                new IntentQaSnapshot("qa-2", "Q2", "A2", At.AddMinutes(-5)),
                new IntentQaSnapshot("qa-1", "Q1", "A1", At.AddHours(-1)),
            ],
            ReviewList: [
                new IntentReviewSnapshot("rev-1", "Reason1", "Note1", At.AddHours(-2)),
            ],
            UpdatedAt: At);
        var window = new IntentWindow([intent]);

        var result = counter.Count(window);

        // parts joined: current + qa-1 + qa-2 + rev-1 (qa ordered by CreatedAt ASC)
        string[] parts =
        [
            "current",
            "Q: Q1\nA: A1",
            "Q: Q2\nA: A2",
            "Reason: Reason1\nNote: Note1",
        ];
        var expected = string.Join("\n", parts);
        result.TotalTokens.Should().Be(expected.Length);
    }

    [Fact(DisplayName = "Multiple intents суммируются")]
    public void Multiple_intents_summed()
    {
        var counter = new ContextTokenCounter(new LengthTokenizer());
        var window = new IntentWindow(
        [
            SimpleIntent("intent-1", "aaa"),
            SimpleIntent("intent-2", "bbbb"),
        ]);

        var result = counter.Count(window);

        result.TotalTokens.Should().Be(3 + 4);
        result.PerIntent.Should().HaveCount(2);
    }

    private static IntentInWindow SimpleIntent(string id, string text) =>
        new(id, text, [], [], [], At);

    private sealed class LengthTokenizer : ITokenizer
    {
        public int CountTokens(string text) => text?.Length ?? 0;
    }
}
