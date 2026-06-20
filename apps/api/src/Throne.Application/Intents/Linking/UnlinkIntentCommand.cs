namespace Throne.Application.Intents.Linking;

public sealed record UnlinkIntentCommand(string FromId, string ToId);
