namespace Throne.Application.Ports;

/// <summary>
/// Token-counting port. The Application layer counts tokens of /dream context to surface
/// «накоплено N токенов» in UI; the actual BPE encoding lives in Infrastructure.
/// </summary>
public interface ITokenizer
{
    int CountTokens(string text);
}
