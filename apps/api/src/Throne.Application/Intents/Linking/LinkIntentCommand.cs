using Throne.Domain.Intents.Linking;

namespace Throne.Application.Intents.Linking;

public sealed record LinkIntentCommand(
    string FromId,
    string ToId,
    string Type,
    IntentLinkAuthor Author,
    string? Rationale);
