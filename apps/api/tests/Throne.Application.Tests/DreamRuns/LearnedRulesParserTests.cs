using FluentAssertions;
using Throne.Application.DreamRuns;

namespace Throne.Application.Tests.DreamRuns;

public class LearnedRulesParserTests
{
    [Fact(DisplayName = "Пустой текст → пустой список")]
    public void Empty_text_returns_empty()
    {
        LearnedRulesParser.Parse(string.Empty).Should().BeEmpty();
    }

    [Fact(DisplayName = "Текст без секции '## Learned rules' → пустой список")]
    public void No_section_returns_empty()
    {
        LearnedRulesParser.Parse("# Title\n\nNothing here.\n").Should().BeEmpty();
    }

    [Fact(DisplayName = "Bullets под секцией читаются по порядку")]
    public void Bullets_are_read_in_order()
    {
        var text = "# Header\n\n## Learned rules\n\n- Rule one\n- Rule two with comma, ok\n";
        var rules = LearnedRulesParser.Parse(text);

        rules.Select(r => r.RuleText).Should().Equal("Rule one", "Rule two with comma, ok");
    }

    [Fact(DisplayName = "Парсер останавливается на следующей секции H2")]
    public void Parser_stops_on_next_h2()
    {
        var text = "## Learned rules\n\n- A\n\n## Other\n\n- B\n";
        var rules = LearnedRulesParser.Parse(text);

        rules.Should().HaveCount(1).And.ContainSingle().Which.RuleText.Should().Be("A");
    }

    [Fact(DisplayName = "Линии без префикса '- ' игнорируются")]
    public void Lines_without_bullet_prefix_are_ignored()
    {
        var text = "## Learned rules\n\nIntro line.\n- Real bullet\nAnother prose line.\n";
        var rules = LearnedRulesParser.Parse(text);

        rules.Should().ContainSingle().Which.RuleText.Should().Be("Real bullet");
    }

    [Fact(DisplayName = "Round-trip с LearnedRulesInjector сохраняет текст правила")]
    public void Roundtrip_with_injector()
    {
        var injected = LearnedRulesInjector.Inject(string.Empty, "round trip rule");
        var rules = LearnedRulesParser.Parse(injected);

        rules.Should().ContainSingle().Which.RuleText.Should().Be("round trip rule");
    }
}
