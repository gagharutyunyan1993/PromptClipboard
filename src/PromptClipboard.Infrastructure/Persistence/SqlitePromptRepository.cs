namespace PromptClipboard.Infrastructure.Persistence;

using Microsoft.Data.Sqlite;
using PromptClipboard.Domain.Entities;
using PromptClipboard.Domain.Interfaces;

public sealed class SqlitePromptRepository : IPromptRepository
{
    private readonly SqliteConnectionFactory _factory;

    public SqlitePromptRepository(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<List<Prompt>> SearchAsync(string query, string? tagFilter = null, string? langFilter = null, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();

        var hasQuery = !string.IsNullOrWhiteSpace(query);
        var hasTag = !string.IsNullOrWhiteSpace(tagFilter);
        var hasLang = !string.IsNullOrWhiteSpace(langFilter);

        string sql;
        if (hasQuery)
        {
            sql = """
                SELECT p.* FROM prompts p
                INNER JOIN prompts_fts ON prompts_fts.rowid = p.id
                WHERE prompts_fts MATCH @query
            """;
            if (hasTag)
                sql += " AND EXISTS (SELECT 1 FROM json_each(p.tags_json) WHERE LOWER(json_each.value) = LOWER(@tag))";
            if (hasLang)
                sql += " AND p.lang = @lang";
            sql += " ORDER BY bm25(prompts_fts, 10.0, 5.0, 1.0), p.is_pinned DESC, p.use_count DESC LIMIT 50";
        }
        else
        {
            sql = "SELECT p.* FROM prompts p WHERE 1=1";
            if (hasTag)
                sql += " AND EXISTS (SELECT 1 FROM json_each(p.tags_json) WHERE LOWER(json_each.value) = LOWER(@tag))";
            if (hasLang)
                sql += " AND p.lang = @lang";
            sql += " ORDER BY p.is_pinned DESC, COALESCE(p.last_used_at, p.created_at) DESC, p.use_count DESC LIMIT 50";
        }

        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (hasQuery) cmd.Parameters.AddWithValue("@query", query);
        if (hasTag) cmd.Parameters.AddWithValue("@tag", tagFilter!.ToLowerInvariant());
        if (hasLang) cmd.Parameters.AddWithValue("@lang", langFilter!);

        return await ReadPromptsAsync(cmd, ct);
    }

    public async Task<List<Prompt>> GetPinnedAsync(CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM prompts WHERE is_pinned = 1 ORDER BY title";
        return await ReadPromptsAsync(cmd, ct);
    }

    public async Task<List<Prompt>> GetRecentAsync(int limit = 10, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM prompts ORDER BY COALESCE(last_used_at, created_at) DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", limit);
        return await ReadPromptsAsync(cmd, ct);
    }

    public async Task<Prompt?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM prompts WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        var list = await ReadPromptsAsync(cmd, ct);
        return list.FirstOrDefault();
    }

    public async Task<long> CreateAsync(Prompt prompt, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO prompts (title, body, tags_json, tags_text, folder, created_at, updated_at, last_used_at, use_count, is_pinned, lang, model_hint, version_parent_id)
            VALUES (@title, @body, @tags_json, @tags_text, @folder, @created_at, @updated_at, @last_used_at, @use_count, @is_pinned, @lang, @model_hint, @version_parent_id);
            SELECT last_insert_rowid();
        """;
        AddPromptParams(cmd, prompt);
        var result = await Task.Run(() => cmd.ExecuteScalar(), ct);
        return (long)(result ?? 0);
    }

    public async Task UpdateAsync(Prompt prompt, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE prompts SET
                title = @title, body = @body, tags_json = @tags_json, tags_text = @tags_text,
                folder = @folder, updated_at = @updated_at, last_used_at = @last_used_at,
                use_count = @use_count, is_pinned = @is_pinned, lang = @lang,
                model_hint = @model_hint, version_parent_id = @version_parent_id
            WHERE id = @id
        """;
        cmd.Parameters.AddWithValue("@id", prompt.Id);
        AddPromptParams(cmd, prompt);
        await Task.Run(() => cmd.ExecuteNonQuery(), ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM prompts WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await Task.Run(() => cmd.ExecuteNonQuery(), ct);
    }

    public async Task MarkUsedAsync(long id, DateTime usedAt, CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE prompts SET
                last_used_at = @used_at,
                use_count = use_count + 1,
                updated_at = @used_at
            WHERE id = @id
        """;
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@used_at", usedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        await Task.Run(() => cmd.ExecuteNonQuery(), ct);
    }

    public async Task<List<Prompt>> GetAllAsync(CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM prompts ORDER BY title";
        return await ReadPromptsAsync(cmd, ct);
    }

    public async Task<int> GetCountAsync(CancellationToken ct = default)
    {
        using var conn = _factory.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(1) FROM prompts";
        var result = await Task.Run(() => cmd.ExecuteScalar(), ct);
        return Convert.ToInt32(result);
    }

    private static void AddPromptParams(SqliteCommand cmd, Prompt p)
    {
        cmd.Parameters.AddWithValue("@title", p.Title);
        cmd.Parameters.AddWithValue("@body", p.Body);
        cmd.Parameters.AddWithValue("@tags_json", p.TagsJson);
        cmd.Parameters.AddWithValue("@tags_text", p.TagsText);
        cmd.Parameters.AddWithValue("@folder", p.Folder);
        cmd.Parameters.AddWithValue("@created_at", p.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@updated_at", p.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("@last_used_at", p.LastUsedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@use_count", p.UseCount);
        cmd.Parameters.AddWithValue("@is_pinned", p.IsPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("@lang", p.Lang);
        cmd.Parameters.AddWithValue("@model_hint", p.ModelHint);
        cmd.Parameters.AddWithValue("@version_parent_id", p.VersionParentId ?? (object)DBNull.Value);
    }

    private static async Task<List<Prompt>> ReadPromptsAsync(SqliteCommand cmd, CancellationToken ct)
    {
        var prompts = new List<Prompt>();
        await Task.Run(() =>
        {
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                prompts.Add(MapPrompt(reader));
            }
        }, ct);
        return prompts;
    }

    private static Prompt MapPrompt(SqliteDataReader r)
    {
        return new Prompt
        {
            Id = r.GetInt64(r.GetOrdinal("id")),
            Title = r.GetString(r.GetOrdinal("title")),
            Body = r.GetString(r.GetOrdinal("body")),
            TagsJson = r.GetString(r.GetOrdinal("tags_json")),
            TagsText = r.GetString(r.GetOrdinal("tags_text")),
            Folder = r.GetString(r.GetOrdinal("folder")),
            CreatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("created_at"))),
            UpdatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("updated_at"))),
            LastUsedAt = r.IsDBNull(r.GetOrdinal("last_used_at")) ? null : DateTime.Parse(r.GetString(r.GetOrdinal("last_used_at"))),
            UseCount = r.GetInt32(r.GetOrdinal("use_count")),
            IsPinned = r.GetInt32(r.GetOrdinal("is_pinned")) == 1,
            Lang = r.GetString(r.GetOrdinal("lang")),
            ModelHint = r.GetString(r.GetOrdinal("model_hint")),
            VersionParentId = r.IsDBNull(r.GetOrdinal("version_parent_id")) ? null : r.GetInt64(r.GetOrdinal("version_parent_id"))
        };
    }
}
