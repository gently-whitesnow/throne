namespace Throne.Application.Terminals;

/// <summary>
/// Shared config for the per-session hook callback URL ([ADR-0034]): the local API base every
/// vendor's <c>Stop</c> hook posts back to. Vendor-neutral — both the Claude settings file and the
/// Codex inline override build the same <c>/terminal/hooks/{event}</c> endpoint from it.
/// </summary>
public sealed class SessionHookOptions
{
    public const string ApiBaseUrlKey = "Throne:ApiBaseUrl";
    public const string DefaultApiBaseUrl = "http://localhost:5008";

    public string ApiBaseUrl { get; init; } = DefaultApiBaseUrl;
}
