namespace Throne.Domain.TaskTrackers;

/// <summary>
/// Immutable snapshot of an attached card's content as of the last successful pull (ADR-0052). Read-only
/// context — never written back upstream. <see cref="CardVersion"/> is the opaque provider revision/etag
/// captured with the snapshot; <see cref="FetchedAt"/> is when this snapshot was pulled.
/// </summary>
public sealed record CardSnapshot(
    string Title,
    string? Description,
    string? ColumnTitle,
    bool Archived,
    string? CardVersion,
    DateTimeOffset FetchedAt);
