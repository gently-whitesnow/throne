namespace Throne.Application.Manifest;

public sealed class InMemoryUserPromptSeedProvider(UserPromptSeed seed) : IUserPromptSeedProvider
{
    public UserPromptSeed Current { get; } = seed ?? throw new ArgumentNullException(nameof(seed));
}
