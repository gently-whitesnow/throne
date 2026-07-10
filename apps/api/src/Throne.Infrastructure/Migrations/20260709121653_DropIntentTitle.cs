using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Throne.Infrastructure.Migrations
{
    /// <summary>
    /// Drops the retired <c>intents.title</c> column and rebuilds the FTS5 index over
    /// <c>intents.text</c> only. The virtual table and its triggers are raw SQL (EF cannot model
    /// FTS5). Triggers are dropped before the column so SQLite's <c>ALTER TABLE … DROP COLUMN</c>
    /// is not blocked by a trigger referencing <c>title</c>; the index is then recreated without a
    /// title column and repopulated from the surviving bodies.
    /// </summary>
    public partial class DropIntentTitle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS intents_fts_ai;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS intents_fts_ad;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS intents_fts_au;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS intents_fts;");

            migrationBuilder.Sql("ALTER TABLE intents DROP COLUMN title;");

            migrationBuilder.Sql(
                "CREATE VIRTUAL TABLE intents_fts USING fts5("
                + "intent_id UNINDEXED, text, "
                + "tokenize='unicode61 remove_diacritics 2');");

            migrationBuilder.Sql(
                "CREATE TRIGGER intents_fts_ai AFTER INSERT ON intents BEGIN "
                + "INSERT INTO intents_fts(intent_id, text) VALUES (new.id, new.text); "
                + "END;");

            migrationBuilder.Sql(
                "CREATE TRIGGER intents_fts_ad AFTER DELETE ON intents BEGIN "
                + "DELETE FROM intents_fts WHERE intent_id = old.id; "
                + "END;");

            migrationBuilder.Sql(
                "CREATE TRIGGER intents_fts_au AFTER UPDATE OF text ON intents BEGIN "
                + "UPDATE intents_fts SET text = new.text WHERE intent_id = old.id; "
                + "END;");

            migrationBuilder.Sql(
                "INSERT INTO intents_fts(intent_id, text) SELECT id, text FROM intents;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS intents_fts_ai;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS intents_fts_ad;");
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS intents_fts_au;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS intents_fts;");

            migrationBuilder.Sql("ALTER TABLE intents ADD COLUMN title TEXT;");

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
    }
}
