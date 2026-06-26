using Throne.Domain.Repositories;
using Throne.Domain.Tags;
using Throne.Infrastructure.EfCore.Rows;
using Tag = Throne.Domain.Tags.Tag;

namespace Throne.Infrastructure.EfCore.Mappers;

internal static class TagRowMapper
{
    public static TagRow ToRow(Tag tag) => new()
    {
        Id = tag.Id.Value,
        Name = tag.Name,
        CurrentVersion = tag.CurrentVersion,
        CreatedAt = tag.CreatedAt,
        UpdatedAt = tag.UpdatedAt,
        // last_attached_at is owned by EfTagAttachmentToucher — never written from the
        // aggregate snapshot, so initial row inserts leave it null.
        LastAttachedAt = null,
        DefaultRepositories = tag.DefaultRepositories.Count == 0
            ? []
            : [.. tag.DefaultRepositories.Select(ToPayload)],
    };

    public static Tag ToDomain(TagRow row) => Tag.Restore(
        id: new TagId(row.Id),
        name: row.Name,
        currentVersion: row.CurrentVersion,
        createdAt: row.CreatedAt,
        updatedAt: row.UpdatedAt,
        defaultRepositories: row.DefaultRepositories.Count == 0
            ? []
            : [.. row.DefaultRepositories.Select(ToDomain)]);

    public static TagDefaultRepositoryPayload ToPayload(TagDefaultRepository entry) => new()
    {
        Provider = entry.Coordinate.Provider,
        Host = entry.Coordinate.Host,
        Owner = entry.Coordinate.Owner,
        Repo = entry.Coordinate.Repo,
        ProjectId = entry.Coordinate.ProjectId,
        DefaultBranch = entry.DefaultBranch,
    };

    private static TagDefaultRepository ToDomain(TagDefaultRepositoryPayload payload) =>
        new(new RepoCoordinate(
            payload.Provider,
            payload.Owner,
            payload.Repo,
            HostOrDefault(payload.Provider, payload.Host),
            payload.ProjectId), payload.DefaultBranch);

    private static string? HostOrDefault(string provider, string? host) =>
        string.IsNullOrWhiteSpace(host) && provider == GitProviderNames.GitHub
            ? GitProviderHostDefaults.GitHub
            : host;
}
