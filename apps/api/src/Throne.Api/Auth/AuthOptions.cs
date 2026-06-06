namespace Throne.Api.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Имя claim, из которого берётся идентификатор пользователя — стандартный
    /// OIDC <c>sub</c>. См. ADR-0016.
    /// </summary>
    public const string UserIdClaim = "sub";

    public AuthMode Mode { get; set; } = AuthMode.Disabled;

    /// <summary>
    /// OIDC authority внешнего Authorization Server'а. JWKS подтягивается
    /// автоматически из <c>{Authority}/.well-known/openid-configuration</c>.
    /// Конкретный AS (open-source или приватный) — деталь деплоя, throne про
    /// это ничего не знает.
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// Прямой URL discovery-метаданных. Используется, если <see cref="Authority"/>
    /// не задан.
    /// </summary>
    public string? MetadataAddress { get; set; }

    public bool RequireHttpsMetadata { get; set; } = true;
}

public enum AuthMode
{
    Disabled = 0,
    Jwt = 1,
}
