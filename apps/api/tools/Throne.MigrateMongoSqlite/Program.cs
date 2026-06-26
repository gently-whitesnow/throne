namespace Throne.MigrateMongoSqlite;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (!MigrationOptions.TryParse(args, out var options, out var error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(MigrationOptions.Usage);
            return 2;
        }

        try
        {
            var summary = await MongoSqliteMigrationRunner.RunAsync(options, CancellationToken.None);
            Console.WriteLine(summary.Format());
            return 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
