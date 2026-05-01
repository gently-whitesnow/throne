namespace Throne.Application.Intents;

public sealed record Ack(string IntentId, int CurrentVersion, bool Accepted = true);
