namespace Throne.Application.Auth;

/// <summary>
/// Только что выпущенный PAT. <see cref="Plaintext"/> возвращается клиенту ровно один
/// раз; в БД сохраняется только <see cref="HashSha256"/> и <see cref="LastFour"/>.
/// </summary>
public sealed record PersonalAccessTokenSecret(
    string Plaintext,
    string HashSha256,
    string LastFour);
