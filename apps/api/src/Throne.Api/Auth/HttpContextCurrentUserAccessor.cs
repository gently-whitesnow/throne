using Microsoft.AspNetCore.Http;
using Throne.Application.Auth;

namespace Throne.Api.Auth;

internal sealed class HttpContextCurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    : ICurrentUserAccessor
{
    public string UserId
    {
        get
        {
            var ctx = httpContextAccessor.HttpContext
                ?? throw new InvalidOperationException(
                    "ICurrentUserAccessor запрошен вне HTTP-запроса.");

            var user = ctx.User
                ?? throw new InvalidOperationException(
                    "HttpContext.User не инициализирован.");

            var value = user.FindFirst(AuthOptions.UserIdClaim)?.Value;
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"В JWT отсутствует claim '{AuthOptions.UserIdClaim}'.");
            }

            return value;
        }
    }
}
