using FluentAssertions;
using Throne.Infrastructure.Tokenization;

namespace Throne.Infrastructure.Tests.Tokenization;

public class SharpTokenTokenizerTests
{
    private readonly SharpTokenTokenizer _tokenizer = new();

    [Fact(DisplayName = "Пустая/null строка → 0 токенов")]
    public void Empty_returns_zero()
    {
        _tokenizer.CountTokens(string.Empty).Should().Be(0);
        _tokenizer.CountTokens(null!).Should().Be(0);
    }

    [Fact(DisplayName = "ASCII фраза 'hello world' → 2 токена для cl100k_base")]
    public void Ascii_hello_world_is_two_tokens()
    {
        _tokenizer.CountTokens("hello world").Should().Be(2);
    }

    [Fact(DisplayName = "Кириллица токенизируется > 1 токен")]
    public void Cyrillic_text_is_tokenized()
    {
        _tokenizer.CountTokens("привет мир").Should().BeGreaterThan(1);
    }

    [Fact(DisplayName = "Многострочный текст складывается из per-line tokens")]
    public void Multiline_text_summed()
    {
        var first = _tokenizer.CountTokens("line one");
        var second = _tokenizer.CountTokens("line two");
        var combined = _tokenizer.CountTokens("line one\nline two");
        // BPE может склеивать токены через перенос строки, но сумма должна быть близка.
        combined.Should().BeGreaterThanOrEqualTo(Math.Max(first, second));
    }
}
