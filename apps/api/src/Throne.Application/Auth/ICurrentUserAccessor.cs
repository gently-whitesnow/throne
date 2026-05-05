namespace Throne.Application.Auth;

public interface ICurrentUserAccessor
{
    string UserId { get; }
}
