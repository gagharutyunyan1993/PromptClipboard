namespace PromptClipboard.Infrastructure.Persistence;

using System.IO;
using Microsoft.Data.Sqlite;

public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;
    private readonly bool _isSharedMemory;

    public SqliteConnectionFactory(string dbPath)
    {
        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    /// <summary>
    /// For in-memory testing. Keeps the sentinel connection open so the shared
    /// in-memory database survives across multiple CreateConnection() / Dispose() cycles.
    /// </summary>
    public SqliteConnectionFactory(SqliteConnection sharedConnection)
    {
        _connectionString = sharedConnection.ConnectionString;
        _sentinelConnection = sharedConnection;
        _isSharedMemory = true;
    }

    private readonly SqliteConnection? _sentinelConnection;

    public SqliteConnection CreateConnection()
    {
        if (_isSharedMemory)
        {
            // Return a new connection sharing the same in-memory database.
            // The sentinel connection keeps the database alive even when
            // this connection is disposed.
            var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA foreign_keys=ON;";
            cmd.ExecuteNonQuery();
            return conn;
        }

        var c = new SqliteConnection(_connectionString);
        c.Open();
        using var pragmaCmd = c.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA foreign_keys=ON; PRAGMA journal_mode=WAL;";
        pragmaCmd.ExecuteNonQuery();
        return c;
    }
}
