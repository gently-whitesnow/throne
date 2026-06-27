namespace Throne.Infrastructure.Security;

/// <summary>
/// Symmetric protection for secrets stored at rest in the local SQLite database (ADR-0047/0029) —
/// today only task-tracker API tokens. Local-first, single operator: the goal is «no plaintext
/// secrets in the DB file», not defence against an attacker who already owns the home directory (the
/// key lives next to the database). <see cref="Protect"/> / <see cref="Unprotect"/> round-trip a
/// UTF-8 string through an opaque base64 envelope.
/// </summary>
internal interface ISecretProtector
{
    string Protect(string plaintext);

    string Unprotect(string protectedValue);
}
