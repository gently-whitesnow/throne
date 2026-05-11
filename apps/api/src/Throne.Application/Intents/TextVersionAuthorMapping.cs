using Throne.Domain.Intents.Training;
using Throne.Domain.TextVersions;

namespace Throne.Application.Intents;

internal static class TextVersionAuthorMapping
{
    public static IntentTrainingAuthor ToTrainingAuthor(TextVersionAuthor author) => author switch
    {
        TextVersionAuthor.User => IntentTrainingAuthor.User,
        TextVersionAuthor.Agent => IntentTrainingAuthor.Agent,
        TextVersionAuthor.System => IntentTrainingAuthor.System,
        _ => throw new InvalidOperationException($"Unknown author: {author}."),
    };
}
