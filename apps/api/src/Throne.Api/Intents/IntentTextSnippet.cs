namespace Throne.Api.Intents;

internal static class IntentTextSnippet
{
    public static string Cut(string text, int max) =>
        text.Length <= max ? text : text[..max];
}
