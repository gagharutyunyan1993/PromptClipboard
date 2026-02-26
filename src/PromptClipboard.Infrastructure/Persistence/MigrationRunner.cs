namespace PromptClipboard.Infrastructure.Persistence;

using Microsoft.Data.Sqlite;
using Serilog;

public sealed class MigrationRunner
{
    private readonly SqliteConnectionFactory _factory;
    private readonly ILogger _log;
    private readonly List<(string Version, string Sql)> _migrations = [];

    public MigrationRunner(SqliteConnectionFactory factory, ILogger log)
    {
        _factory = factory;
        _log = log;
        RegisterMigrations();
    }

    private void RegisterMigrations()
    {
        _migrations.Add(("V001", Migrations.V001_InitialSchema));
        _migrations.Add(("V002", Migrations.V002_AddFts5Index));
    }

    public void RunAll()
    {
        using var conn = _factory.CreateConnection();

        using var createCmd = conn.CreateCommand();
        createCmd.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version TEXT PRIMARY KEY,
                applied_at TEXT NOT NULL DEFAULT (datetime('now'))
            );
        """;
        createCmd.ExecuteNonQuery();

        foreach (var (version, sql) in _migrations)
        {
            using var checkCmd = conn.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(1) FROM schema_migrations WHERE version = @v";
            checkCmd.Parameters.AddWithValue("@v", version);
            var applied = (long)(checkCmd.ExecuteScalar() ?? 0) > 0;

            if (applied)
            {
                _log.Debug("Migration {Version} already applied", version);
                continue;
            }

            _log.Information("Applying migration {Version}...", version);
            using var tx = conn.BeginTransaction();
            try
            {
                using var migCmd = conn.CreateCommand();
                migCmd.Transaction = tx;
                migCmd.CommandText = sql;
                migCmd.ExecuteNonQuery();

                using var insertCmd = conn.CreateCommand();
                insertCmd.Transaction = tx;
                insertCmd.CommandText = "INSERT INTO schema_migrations (version) VALUES (@v)";
                insertCmd.Parameters.AddWithValue("@v", version);
                insertCmd.ExecuteNonQuery();

                tx.Commit();
                _log.Information("Migration {Version} applied successfully", version);
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }
}
