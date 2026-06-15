using MongoDB.Driver;

namespace Throne.Infrastructure.Mongo;

/// <summary>
/// Tiny helper around <see cref="MongoWriteException"/> classification.
/// Several repositories race-insert on a unique index and want to fall back to
/// "re-read the winner" on E11000; this keeps the predicate in one place so we
/// stop diverging on Category-vs-Code spellings.
/// </summary>
internal static class MongoWriteExceptionHelper
{
    private const int DuplicateKeyCode = 11000;

    public static bool IsDuplicateKey(MongoWriteException ex)
    {
        ArgumentNullException.ThrowIfNull(ex);
        var err = ex.WriteError;
        return err is not null
               && (err.Category == ServerErrorCategory.DuplicateKey || err.Code == DuplicateKeyCode);
    }
}
