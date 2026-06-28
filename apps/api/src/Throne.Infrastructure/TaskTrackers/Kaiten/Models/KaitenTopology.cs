namespace Throne.Infrastructure.TaskTrackers.Kaiten.Models;

/// <summary>
/// A Kaiten space — the top-level container of boards. <c>GET /spaces</c> returns each space with its
/// <see cref="Boards"/> nested inline, so the whole topology is one call rather than a per-space fan-out.
/// </summary>
internal sealed record KaitenSpace(
    long Id,
    string Title,
    string? Description,
    bool Archived = false,
    IReadOnlyList<KaitenBoard>? Boards = null);

/// <summary>A board within a space; the home of columns, lanes and cards.</summary>
internal sealed record KaitenBoard(long Id, string Title, string? Description, long SpaceId);

/// <summary>A board column (workflow stage). <see cref="Sort"/> is the left-to-right order.</summary>
internal sealed record KaitenColumn(long Id, string Title, long BoardId, int Sort);

/// <summary>A board lane (swimlane). <see cref="Sort"/> is the top-to-bottom order.</summary>
internal sealed record KaitenLane(long Id, string Title, long BoardId, int Sort);
