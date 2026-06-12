namespace Throne.Application.Terminals;

public sealed class ClaudeSessionHookOptions
{
    public const string ApiBaseUrlKey = "Throne:ApiBaseUrl";
    public const string DefaultApiBaseUrl = "http://localhost:5008";

    public string ApiBaseUrl { get; init; } = DefaultApiBaseUrl;
}
