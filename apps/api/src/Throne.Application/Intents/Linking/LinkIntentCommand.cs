using Throne.Domain.Intents.Linking;

namespace Throne.Application.Intents.Linking;

public sealed record LinkIntentCommand(
    string FromId,
    string ToId,
    bool Blocking,
    IntentLinkAuthor Author,
    string? Rationale);
