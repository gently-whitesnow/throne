namespace Throne.Api.Auth;

/// <summary>
/// Audience-валидация JWT (ADR-0012, ADR-0016). Биндится из той же
/// конфигурационной секции <c>Auth</c>, что и <see cref="AuthOptions"/>,
/// — wire-формат остаётся плоским: <c>Auth:Issuer</c>, <c>Auth:Audience</c>,
/// <c>Auth:AdditionalAudiences</c>.
/// </summary>
public sealed class AuthAudienceOptions
{
    public string? Issuer { get; set; }

    /// <summary>
    /// Основной audience входящих JWT — для REST-сессий (например, <c>"throne"</c>).
    /// Совместимость со старой конфигурацией.
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Дополнительные допустимые audience для OAuth-токенов, выпущенных внешним AS
    /// на конкретный ресурс (ADR-0016). Каждое значение совпадает с публичным URL
    /// соответствующего MCP-эндпоинта (например, <c>"https://example.org/mcp"</c>)
    /// и становится <c>aud</c> access-token'а. При отсутствии массива OAuth-токены
    /// не принимаются.
    /// </summary>
    public List<string> AdditionalAudiences { get; set; } = new();
}
