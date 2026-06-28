using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Throne.Application.Search;

namespace Throne.Infrastructure.EfCore.Search;

/// <summary>
/// FTS5/BM25 implementation of the search-core read port (ADR-0050). Matches against the
/// <c>intents_fts</c> virtual table, orders by <c>bm25()</c> (most relevant first) and
/// builds the highlighted snippet with the FTS5 <c>snippet()</c> auxiliary function. Uses
/// raw ADO over the EF connection because FTS5 MATCH / bm25 / snippet have no LINQ surface.
/// </summary>
internal sealed class EfIntentSearchReader(
    IDbContextFactory<ThroneDbContext> contextFactory,
    EfSessionAccessor sessions)
    : EfRepositoryBase(contextFactory, sessions), IIntentSearchReader
{
    // Columns: 0=intent_id (UNINDEXED), 1=title, 2=text. bm25 weights boost title matches so
    // a title hit outranks a body hit; snippet() builds the highlighted body excerpt (col 2).
    private const string SearchSql =
        "SELECT intent_id, snippet(intents_fts, 2, $open, $close, '…', 12) AS snippet "
        + "FROM intents_fts WHERE intents_fts MATCH $match "
        + "ORDER BY bm25(intents_fts, 1.0, 5.0, 1.0) LIMIT $limit;";

    public Task<IReadOnlyList<IntentSearchHit>> SearchAsync(string query, int limit, CancellationToken ct)
    {
        var match = Fts5MatchQuery.Build(query);
        if (match is null || limit <= 0)
        {
            return Task.FromResult<IReadOnlyList<IntentSearchHit>>([]);
        }

        return ReadAsync<IReadOnlyList<IntentSearchHit>>(async (ctx, c) =>
        {
            var connection = ctx.Database.GetDbConnection();
            var openedHere = connection.State != ConnectionState.Open;
            if (openedHere)
            {
                await connection.OpenAsync(c);
            }
            try
            {
                await using var command = (SqliteCommand)connection.CreateCommand();
                command.CommandText = SearchSql;
                command.Parameters.AddWithValue("$match", match);
                command.Parameters.AddWithValue("$open", IntentSearchMarkers.Open);
                command.Parameters.AddWithValue("$close", IntentSearchMarkers.Close);
                command.Parameters.AddWithValue("$limit", limit);

                var hits = new List<IntentSearchHit>();
                await using var reader = await command.ExecuteReaderAsync(c);
                while (await reader.ReadAsync(c))
                {
                    hits.Add(new IntentSearchHit(reader.GetString(0), reader.GetString(1)));
                }
                return hits;
            }
            finally
            {
                if (openedHere)
                {
                    await connection.CloseAsync();
                }
            }
        }, ct);
    }
}
