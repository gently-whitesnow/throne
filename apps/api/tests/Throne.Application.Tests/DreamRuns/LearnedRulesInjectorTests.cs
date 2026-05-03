using FluentAssertions;
using Throne.Application.DreamRuns;

namespace Throne.Application.Tests.DreamRuns;

public class LearnedRulesInjectorTests
{
    [Fact(DisplayName = "Пустой текст → секция создаётся с rule")]
    public void Empty_text_creates_section()
    {
        var result = LearnedRulesInjector.Inject(string.Empty, "не делать unrelated refactor");
        result.Should().Be("## Learned rules\n\n- не делать unrelated refactor\n");
    }

    [Fact(DisplayName = "Текст без секции → секция добавляется в конец")]
    public void Adds_section_when_missing()
    {
        var result = LearnedRulesInjector.Inject("Header text\n", "rule X");
        result.Should().Contain("Header text");
        result.Should().EndWith("## Learned rules\n\n- rule X\n");
    }

    [Fact(DisplayName = "Существующая секция получает новый bullet вверху")]
    public void Existing_section_prepends_bullet()
    {
        var input = "Intro\n\n## Learned rules\n\n- old rule\n";
        var result = LearnedRulesInjector.Inject(input, "new rule");
        result.Should().Contain("- new rule");
        result.Should().Contain("- old rule");
        result.IndexOf("- new rule", StringComparison.Ordinal)
            .Should().BeLessThan(result.IndexOf("- old rule", StringComparison.Ordinal));
    }
}
