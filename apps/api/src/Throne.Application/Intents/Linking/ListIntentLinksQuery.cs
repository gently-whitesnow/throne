using Throne.Application.Ports;

namespace Throne.Application.Intents.Linking;

public sealed record ListIntentLinksQuery(
    string IntentId,
    IntentLinkDirection? Direction,
    string? Type,
    int Limit,
    string? Cursor);
