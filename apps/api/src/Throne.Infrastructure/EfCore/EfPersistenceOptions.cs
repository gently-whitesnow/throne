using Throne.Infrastructure.Git;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// SQLite backend settings (active only when <c>Persistence:Provider=sqlite</c>).
/// <para>
/// Default <see cref="DataSource"/> is <c>~/.throne/throne.db</c> — a single local file
/// under the operator's home, matching the local-first runtime (ADR-0027/0029). The
/// leading <c>~</c> is expanded against the user profile; the parent directory is
/// created on startup by the schema initializer.
/// </para>
/// <para>
/// WAL journal mode is enabled on startup. It is a persistent property of the database
/// file, so one PRAGMA on first boot keeps every later connection in WAL — readers stop
/// blocking the single writer.
/// </para>
/// </summary>
public sealed class EfPersistenceOptions
{
    public const string SectionName = "Persistence:Sqlite";

    public string DataSource { get; set; } = "~/.throne/throne.db";

    /// <summary>Absolute on-disk path with a leading <c>~</c> expanded to the home dir.</summary>
    public string ResolveDataSourcePath() => WorkspacePathExpansion.ExpandHome(DataSource);
}
