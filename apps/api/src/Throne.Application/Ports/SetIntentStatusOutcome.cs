using Throne.Domain.Intents;

namespace Throne.Application.Ports;

public abstract record SetIntentStatusOutcome
{
    public sealed record Updated(Intent Intent) : SetIntentStatusOutcome;

    public sealed record NotFound : SetIntentStatusOutcome;

    public sealed record Conflict(int CurrentVersion, string CurrentStatus) : SetIntentStatusOutcome;
}
