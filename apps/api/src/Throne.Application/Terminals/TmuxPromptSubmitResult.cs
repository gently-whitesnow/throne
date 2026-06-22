namespace Throne.Application.Terminals;

/// <summary>
/// Outcome of <see cref="TmuxPromptSubmitConfirmer.ConfirmAsync"/>. <see cref="LastSnapshot"/> is the
/// final capture and is non-null only on failure — used by the diagnostic payload so a lost submit
/// never looks like a successful paste. <see cref="Retries"/> is the number of extra <c>send-keys
/// Enter</c> attempts the confirmer issued (zero on the happy path).
/// </summary>
public sealed record TmuxPromptSubmitResult(bool IsConfirmed, int Retries, string? LastSnapshot)
{
    public static TmuxPromptSubmitResult Confirmed(int retries) => new(true, retries, null);

    public static TmuxPromptSubmitResult Failed(int retries, string snapshot) =>
        new(false, retries, snapshot);
}
