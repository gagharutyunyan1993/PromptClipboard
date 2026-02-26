namespace PromptClipboard.Infrastructure.Persistence;

public static class Migrations
{
    public const string V001_InitialSchema = """
        CREATE TABLE prompts (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            title TEXT NOT NULL,
            body TEXT NOT NULL,
            tags_json TEXT NOT NULL DEFAULT '[]',
            tags_text TEXT NOT NULL DEFAULT '',
            folder TEXT NOT NULL DEFAULT '',
            created_at TEXT NOT NULL DEFAULT (datetime('now')),
            updated_at TEXT NOT NULL DEFAULT (datetime('now')),
            last_used_at TEXT,
            use_count INTEGER NOT NULL DEFAULT 0,
            is_pinned INTEGER NOT NULL DEFAULT 0,
            lang TEXT NOT NULL DEFAULT '',
            model_hint TEXT NOT NULL DEFAULT '',
            version_parent_id INTEGER REFERENCES prompts(id)
        );

        CREATE TABLE prompt_versions (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            prompt_id INTEGER NOT NULL REFERENCES prompts(id) ON DELETE CASCADE,
            body TEXT NOT NULL,
            created_at TEXT NOT NULL DEFAULT (datetime('now')),
            note TEXT NOT NULL DEFAULT ''
        );
    """;

    public const string V002_AddFts5Index = """
        CREATE VIRTUAL TABLE prompts_fts USING fts5(
            title,
            body,
            tags_text,
            content='prompts',
            content_rowid='id'
        );

        -- Populate FTS from existing data
        INSERT INTO prompts_fts(rowid, title, body, tags_text)
            SELECT id, title, body, tags_text FROM prompts;

        -- Sync triggers
        CREATE TRIGGER prompts_ai AFTER INSERT ON prompts BEGIN
            INSERT INTO prompts_fts(rowid, title, body, tags_text)
                VALUES (new.id, new.title, new.body, new.tags_text);
        END;

        CREATE TRIGGER prompts_ad AFTER DELETE ON prompts BEGIN
            INSERT INTO prompts_fts(prompts_fts, rowid, title, body, tags_text)
                VALUES ('delete', old.id, old.title, old.body, old.tags_text);
        END;

        CREATE TRIGGER prompts_au AFTER UPDATE ON prompts BEGIN
            INSERT INTO prompts_fts(prompts_fts, rowid, title, body, tags_text)
                VALUES ('delete', old.id, old.title, old.body, old.tags_text);
            INSERT INTO prompts_fts(rowid, title, body, tags_text)
                VALUES (new.id, new.title, new.body, new.tags_text);
        END;
    """;
}
