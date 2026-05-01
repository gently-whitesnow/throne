using FluentAssertions;
using Throne.Domain.Intents;

namespace Throne.Domain.Tests.Intents;

public class IntentTextSearchTests
{
    [Fact(DisplayName = "Search в пустом тексте возвращает пустой результат")]
    public void Empty_text_returns_no_matches()
    {
        var result = IntentTextSearch.Search(string.Empty, "foo", contextLines: 3, limit: 10);

        result.Matches.Should().BeEmpty();
        result.TotalMatches.Should().Be(0);
    }

    [Fact(DisplayName = "Search возвращает MatchLine/MatchColumn 1-indexed")]
    public void Returns_one_indexed_line_and_column()
    {
        var text = "alpha\nbeta\ngamma";

        var result = IntentTextSearch.Search(text, "amma", contextLines: 0, limit: 10);

        result.TotalMatches.Should().Be(1);
        result.Matches.Should().HaveCount(1);
        var match = result.Matches[0];
        match.MatchLine.Should().Be(3);
        match.MatchColumn.Should().Be(2);
        match.Context.Should().Be("gamma");
        match.ContextStartLine.Should().Be(3);
    }

    [Fact(DisplayName = "Search окно контекста соблюдает границы документа")]
    public void Context_window_clamps_to_document_bounds()
    {
        var text = "l1\nl2\nl3\nl4\nl5";

        var result = IntentTextSearch.Search(text, "l1", contextLines: 2, limit: 10);

        var match = result.Matches.Should().ContainSingle().Subject;
        match.ContextStartLine.Should().Be(1);
        match.Context.Should().Be("l1\nl2\nl3");
    }

    [Fact(DisplayName = "Search с limit < total возвращает TotalMatches и обрезанный список")]
    public void Limit_caps_matches_but_total_reflects_full_count()
    {
        var text = "x\nx\nx\nx\nx\nx";

        var result = IntentTextSearch.Search(text, "x", contextLines: 0, limit: 2);

        result.Matches.Should().HaveCount(2);
        result.TotalMatches.Should().Be(6);
    }

    [Fact(DisplayName = "Search применяет server max limit = 50")]
    public void Limit_capped_at_server_max()
    {
        var text = string.Concat(Enumerable.Repeat("x\n", 60));

        var result = IntentTextSearch.Search(text, "x", contextLines: 0, limit: 100);

        result.Matches.Should().HaveCount(IntentTextSearch.ServerMaxLimit);
        result.TotalMatches.Should().Be(60);
    }

    [Fact(DisplayName = "Search case-sensitive: не находит другой регистр")]
    public void Search_is_case_sensitive()
    {
        var result = IntentTextSearch.Search("Foo bar", "foo", contextLines: 0, limit: 10);

        result.Matches.Should().BeEmpty();
        result.TotalMatches.Should().Be(0);
    }

    [Fact(DisplayName = "Search отвергает пустой query")]
    public void Empty_query_throws()
    {
        var act = () => IntentTextSearch.Search("hello", string.Empty, contextLines: 0, limit: 10);

        act.Should().Throw<ArgumentException>().WithParameterName("query");
    }
}
