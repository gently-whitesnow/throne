namespace Throne.Api.Auth;

internal static class JwtShapeDetector
{
    /// <summary>
    /// Bearer считается JWT, если он состоит ровно из трёх непустых base64url-сегментов,
    /// разделённых точками. Совпадает с RFC 7519 Compact serialization. PAT Throne
    /// имеет префикс <c>tpat_</c> и не содержит точек — различаются однозначно.
    /// </summary>
    public static bool LooksLikeJwt(string token)
    {
        int firstDot = token.IndexOf('.', StringComparison.Ordinal);
        if (firstDot <= 0)
        {
            return false;
        }
        int secondDot = token.IndexOf('.', firstDot + 1);
        if (secondDot <= firstDot + 1 || secondDot >= token.Length - 1)
        {
            return false;
        }
        return token.IndexOf('.', secondDot + 1) < 0;
    }
}
