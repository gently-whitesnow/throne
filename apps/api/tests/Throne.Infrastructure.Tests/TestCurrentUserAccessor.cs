using Throne.Application.Auth;

namespace Throne.Infrastructure.Tests;

internal sealed class TestCurrentUserAccessor(string userId = "user-1") : ICurrentUserAccessor
{
    public string UserId { get; } = userId;
}
