namespace Throne.Api.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Имя claim, из которого берётся внутренний идентификатор пользователя Throne.
    /// Согласовано с auth-gate (см. ADR-0012).
    /// </summary>
    public const string UserIdClaim = "user_id";

    public AuthMode Mode { get; set; } = AuthMode.Disabled;

    /// <summary>
    /// OIDC authority (auth-gate). JWKS подтягивается автоматически из
    /// <c>{Authority}/.well-known/openid-configuration</c>.
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// Прямой URL discovery-метаданных. Используется, если <see cref="Authority"/>
    /// не задан.
    /// </summary>
    public string? MetadataAddress { get; set; }

    public string? Issuer { get; set; }

    public string? Audience { get; set; }

    public bool RequireHttpsMetadata { get; set; } = true;
}

public enum AuthMode
{
    Disabled = 0,
    Jwt = 1,
}
