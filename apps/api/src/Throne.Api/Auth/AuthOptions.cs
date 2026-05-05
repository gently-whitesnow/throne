namespace Throne.Api.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public AuthMode Mode { get; set; } = AuthMode.Disabled;
}

public enum AuthMode
{
    Disabled = 0,
    Jwt = 1,
}
