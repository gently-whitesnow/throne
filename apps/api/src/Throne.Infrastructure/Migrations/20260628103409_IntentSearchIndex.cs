using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Throne.Infrastructure.Migrations
{
    /// <summary>
    /// FTS5 ranked-search index over <c>intents.title</c> + <c>intents.text</c> (search-core,
    /// ADR-0050). The virtual table and its sync triggers are raw SQL only — EF cannot model
    /// FTS5, so they are intentionally absent from the model snapshot. The triggers keep the
    /// index in lock-step with every write path (create / replace / insert / status-append /
    /// title edit / delete) without touching repository code.
    /// </summary>
    public partial class IntentSearchIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE VIRTUAL TABLE intents_fts USING fts5("
                + "intent_id UNINDEXED, title, text, "
                + "tokenize='unicode61 remove_diacritics 2');");

            migrationBuilder.Sql(
                "CREATE TRIGGER intents_fts_ai AFTER INSERT ON intents BEGIN "
                + "INSERT INTO intents_fts(intent_id, title, text) VALUES (new.id, new.title, new.text); "
                + "END;");

            migrationBuilder.Sql(
                "CREATE TRIGGER intents_fts_ad AFTER DELETE ON intents BEGIN "
                + "DELETE FROM intents_fts WHERE intent_id = old.id; "
                + "END;");

            migrationBuilder.Sql(
                "CREATE TRIGGER intents_fts_au AFTER UPDATE OF title, text ON intents BEGIN "
                + "UPDATE intents_fts SET title = new.title, text = new.text WHERE intent_id = old.id; "
                + "END;");

            migrationBuilder.Sql(
                "INSERT INTO intents_fts(intent_id, title, text) SELECT id, title, text FROM intents;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS intents_fts_ai;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS intents_fts_ad;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS intents_fts_au;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS intents_fts;");
        }
    }
}
