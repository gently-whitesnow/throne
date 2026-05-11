using SharpToken;
using Throne.Application.Ports;

namespace Throne.Infrastructure.Tokenization;

/// <summary>
/// SharpToken-backed <see cref="ITokenizer"/> with the cl100k_base encoding (GPT-4 / Claude-grade
/// approximation). Singleton — the encoding's BPE ranks file is expensive to load.
/// </summary>
internal sealed class SharpTokenTokenizer : ITokenizer
{
    private readonly GptEncoding _encoding = GptEncoding.GetEncoding("cl100k_base");

    public int CountTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }
        return _encoding.Encode(text).Count;
    }
}
