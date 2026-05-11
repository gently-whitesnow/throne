namespace Throne.Domain.Intents.Linking;

public enum IntentLinkAuthor
{
    User,
    Agent,
}

public static class IntentLinkAuthorExtensions
{
    public static string ToWire(this IntentLinkAuthor author) => author switch
    {
        IntentLinkAuthor.User => "user",
        IntentLinkAuthor.Agent => "agent",
        _ => throw new InvalidOperationException($"Unknown link author: {author}."),
    };

    public static IntentLinkAuthor FromWire(string wire) => wire switch
    {
        "user" => IntentLinkAuthor.User,
        "agent" => IntentLinkAuthor.Agent,
        _ => throw new InvalidOperationException($"Unknown link author wire value: {wire}."),
    };
}
