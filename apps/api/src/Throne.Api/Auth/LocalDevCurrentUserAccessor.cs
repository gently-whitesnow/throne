using Throne.Application.Auth;

namespace Throne.Api.Auth;

internal sealed class LocalDevCurrentUserAccessor : ICurrentUserAccessor
{
    public string UserId => CurrentUserIds.LocalDev;
}
