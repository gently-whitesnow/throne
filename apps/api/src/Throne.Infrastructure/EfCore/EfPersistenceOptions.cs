using Throne.Infrastructure.Git;

namespace Throne.Infrastructure.EfCore;

/// <summary>
/// SQLite backend settings.
/// <para>
/// Default <see cref="DataSource"/> is <c>~/.throne/throne.db</c> — a single local file
/// under the operator's home, matching the local-first runtime (ADR-0027/0029). The
/// leading <c>~</c> is expanded against the user profile; the parent directory is
/// created on startup by the schema initializer.
/// </para>
/// <para>
/// <see cref="JournalMode"/> is applied on startup. Default <c>WAL</c> — a persistent
/// property of the database file, so one PRAGMA on first boot keeps every later
/// connection in WAL and readers stop blocking the single writer. Override to
/// <c>DELETE</c> when the file lives on a network filesystem (sshfs/NFS/SMB) where
/// WAL's shared-memory mmap is unsupported and corrupts the database.
/// </para>
/// </summary>
public sealed class EfPersistenceOptions
{
    public const string SectionName = "Persistence:Sqlite";

    public string DataSource { get; set; } = "~/.throne/throne.db";

    public string JournalMode { get; set; } = "WAL";

    /// <summary>Absolute on-disk path with a leading <c>~</c> expanded to the home dir.</summary>
    public string ResolveDataSourcePath() => WorkspacePathExpansion.ExpandHome(DataSource);
}
