using Throne.Application.Ports;

namespace Throne.Application.Intents.Linking;

public sealed record ListIntentLinksQuery(
    string IntentId,
    IntentLinkDirection? Direction,
    bool? Blocking,
    int Limit,
    string? Cursor);
