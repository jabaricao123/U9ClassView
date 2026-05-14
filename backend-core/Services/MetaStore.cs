using ClassView.Backend.Models;
using Microsoft.Data.Sqlite;

namespace ClassView.Backend.Services;

public sealed class MetaStore
{
    private readonly string dbPath;
    private readonly SemaphoreSlim initLock = new(1, 1);
    private bool initialized;

    public MetaStore(IHostEnvironment env)
    {
        var root = Directory.GetParent(env.ContentRootPath)?.FullName ?? env.ContentRootPath;
        dbPath = Path.Combine(root, "App_Data", "classview-meta.sqlite");
    }

    public async Task<HashSet<string>> GetFavoritedKeysAsync(string itemType)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var conn = await OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "select item_key from favorite_item where item_type=@item_type";
        cmd.Parameters.AddWithValue("@item_type", itemType);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            keys.Add(reader.GetString(0));
        }
        return keys;
    }

    public async Task<Dictionary<string, ClickInfo>> GetClickStatsAsync(string itemType)
    {
        var result = new Dictionary<string, ClickInfo>(StringComparer.OrdinalIgnoreCase);
        await using var conn = await OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "select item_key, click_count, last_clicked_at from click_stat where item_type=@item_type";
        cmd.Parameters.AddWithValue("@item_type", itemType);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result[reader.GetString(0)] = new ClickInfo
            {
                ClickCount = reader.GetInt32(1),
                LastClickedAt = ParseDate(reader.IsDBNull(2) ? null : reader.GetString(2))
            };
        }
        return result;
    }

    public async Task<Dictionary<string, DateTime?>> GetRecentViewsAsync(string itemType)
    {
        var result = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
        await using var conn = await OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "select item_key, viewed_at from recent_view where item_type=@item_type";
        cmd.Parameters.AddWithValue("@item_type", itemType);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result[reader.GetString(0)] = ParseDate(reader.IsDBNull(1) ? null : reader.GetString(1));
        }
        return result;
    }

    public async Task<Dictionary<string, string>> GetNotesAsync(string itemType)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var conn = await OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "select item_key, note from item_note where item_type=@item_type";
        cmd.Parameters.AddWithValue("@item_type", itemType);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result[reader.GetString(0)] = reader.IsDBNull(1) ? "" : reader.GetString(1);
        }
        return result;
    }

    public async Task<List<object>> ListFavoritesAsync(string? itemType)
    {
        await using var conn = await OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = string.IsNullOrWhiteSpace(itemType)
            ? "select id, item_type, item_key, title, subtitle, extra_json, created_at from favorite_item order by created_at desc"
            : "select id, item_type, item_key, title, subtitle, extra_json, created_at from favorite_item where item_type=@item_type order by created_at desc";
        if (!string.IsNullOrWhiteSpace(itemType))
        {
            cmd.Parameters.AddWithValue("@item_type", itemType);
        }

        var result = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new
            {
                Id = reader.GetInt64(0),
                ItemType = reader.GetString(1),
                ItemKey = reader.GetString(2),
                Title = reader.GetString(3),
                Subtitle = reader.IsDBNull(4) ? "" : reader.GetString(4),
                ExtraJson = reader.IsDBNull(5) ? null : reader.GetString(5),
                CreatedAt = ParseDate(reader.GetString(6))
            });
        }
        return result;
    }

    public async Task<bool> ToggleFavoriteAsync(FavoriteToggleRequest request)
    {
        await using var conn = await OpenAsync();
        await using var exists = conn.CreateCommand();
        exists.CommandText = "select id from favorite_item where item_type=@item_type and item_key=@item_key limit 1";
        exists.Parameters.AddWithValue("@item_type", request.ItemType);
        exists.Parameters.AddWithValue("@item_key", request.ItemKey);
        var id = await exists.ExecuteScalarAsync();

        if (id is null)
        {
            await using var insert = conn.CreateCommand();
            insert.CommandText = @"insert into favorite_item(item_type, item_key, title, subtitle, extra_json, created_at)
                                   values(@item_type, @item_key, @title, @subtitle, @extra_json, current_timestamp)";
            insert.Parameters.AddWithValue("@item_type", request.ItemType);
            insert.Parameters.AddWithValue("@item_key", request.ItemKey);
            insert.Parameters.AddWithValue("@title", request.Title ?? "");
            insert.Parameters.AddWithValue("@subtitle", request.Subtitle ?? "");
            insert.Parameters.AddWithValue("@extra_json", (object?)request.ExtraJson ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync();
            return true;
        }

        await using var delete = conn.CreateCommand();
        delete.CommandText = "delete from favorite_item where item_type=@item_type and item_key=@item_key";
        delete.Parameters.AddWithValue("@item_type", request.ItemType);
        delete.Parameters.AddWithValue("@item_key", request.ItemKey);
        await delete.ExecuteNonQueryAsync();
        return false;
    }

    public async Task SaveNoteAsync(NoteSaveRequest request)
    {
        await using var conn = await OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"insert into item_note(item_type, item_key, note, created_at, updated_at)
                            values(@item_type, @item_key, @note, current_timestamp, current_timestamp)
                            on conflict(item_type, item_key) do update set
                              note=excluded.note,
                              updated_at=current_timestamp";
        cmd.Parameters.AddWithValue("@item_type", request.ItemType);
        cmd.Parameters.AddWithValue("@item_key", request.ItemKey);
        cmd.Parameters.AddWithValue("@note", request.Note ?? "");
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<object>> ListRecentAsync(string? itemType, int top)
    {
        await using var conn = await OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = string.IsNullOrWhiteSpace(itemType)
            ? @"select r.item_type, r.item_key, r.title, r.subtitle, r.extra_json, r.viewed_at,
                       coalesce(c.click_count, 0), c.last_clicked_at
                from recent_view r
                left join click_stat c on r.item_type=c.item_type and r.item_key=c.item_key
                order by r.viewed_at desc limit @top"
            : @"select r.item_type, r.item_key, r.title, r.subtitle, r.extra_json, r.viewed_at,
                       coalesce(c.click_count, 0), c.last_clicked_at
                from recent_view r
                left join click_stat c on r.item_type=c.item_type and r.item_key=c.item_key
                where r.item_type=@item_type
                order by r.viewed_at desc limit @top";
        cmd.Parameters.AddWithValue("@top", top <= 0 ? 100 : top);
        if (!string.IsNullOrWhiteSpace(itemType))
        {
            cmd.Parameters.AddWithValue("@item_type", itemType);
        }

        var result = new List<object>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new
            {
                ItemType = reader.GetString(0),
                ItemKey = reader.GetString(1),
                Title = reader.GetString(2),
                Subtitle = reader.IsDBNull(3) ? "" : reader.GetString(3),
                ExtraJson = reader.IsDBNull(4) ? null : reader.GetString(4),
                ViewedAt = ParseDate(reader.GetString(5)),
                ClickCount = reader.GetInt32(6),
                LastClickedAt = reader.IsDBNull(7) ? null : ParseDate(reader.GetString(7))
            });
        }
        return result;
    }

    public async Task RecordRecentAsync(RecentRecordRequest request)
    {
        await using var conn = await OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"insert into recent_view(item_type, item_key, title, subtitle, extra_json, viewed_at)
                            values(@item_type, @item_key, @title, @subtitle, @extra_json, current_timestamp)
                            on conflict(item_type, item_key) do update set
                              title=excluded.title,
                              subtitle=excluded.subtitle,
                              extra_json=excluded.extra_json,
                              viewed_at=current_timestamp";
        cmd.Parameters.AddWithValue("@item_type", request.ItemType);
        cmd.Parameters.AddWithValue("@item_key", request.ItemKey);
        cmd.Parameters.AddWithValue("@title", request.Title ?? "");
        cmd.Parameters.AddWithValue("@subtitle", request.Subtitle ?? "");
        cmd.Parameters.AddWithValue("@extra_json", (object?)request.ExtraJson ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task IncrementClickAsync(RecentClickRequest request)
    {
        await using var conn = await OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"insert into click_stat(item_type, item_key, click_count, last_clicked_at)
                            values(@item_type, @item_key, 1, current_timestamp)
                            on conflict(item_type, item_key) do update set
                              click_count=click_count + 1,
                              last_clicked_at=current_timestamp";
        cmd.Parameters.AddWithValue("@item_type", request.ItemType);
        cmd.Parameters.AddWithValue("@item_key", request.ItemKey);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<SqliteConnection> OpenAsync()
    {
        await EnsureCreatedAsync();
        var conn = new SqliteConnection("Data Source=" + dbPath);
        await conn.OpenAsync();
        return conn;
    }

    private async Task EnsureCreatedAsync()
    {
        if (initialized)
        {
            return;
        }

        await initLock.WaitAsync();
        try
        {
            if (initialized)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            await using var conn = new SqliteConnection("Data Source=" + dbPath);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = SchemaSql;
            await cmd.ExecuteNonQueryAsync();
            initialized = true;
        }
        finally
        {
            initLock.Release();
        }
    }

    private static DateTime? ParseDate(string? value)
    {
        return DateTime.TryParse(value, out var date) ? date : null;
    }

    private const string SchemaSql = @"
create table if not exists favorite_item (
    id integer primary key autoincrement,
    item_type text not null,
    item_key text not null,
    title text not null,
    subtitle text null,
    extra_json text null,
    created_at text not null default current_timestamp
);

create table if not exists recent_view (
    id integer primary key autoincrement,
    item_type text not null,
    item_key text not null,
    title text not null,
    subtitle text null,
    extra_json text null,
    viewed_at text not null default current_timestamp
);

create table if not exists click_stat (
    id integer primary key autoincrement,
    item_type text not null,
    item_key text not null,
    click_count integer not null default 0,
    last_clicked_at text not null default current_timestamp
);

create table if not exists item_note (
    id integer primary key autoincrement,
    item_type text not null,
    item_key text not null,
    note text null,
    created_at text not null default current_timestamp,
    updated_at text not null default current_timestamp
);

create unique index if not exists ux_favorite_item_type_key on favorite_item(item_type, item_key);
create unique index if not exists ux_recent_view_type_key on recent_view(item_type, item_key);
create unique index if not exists ux_click_stat_type_key on click_stat(item_type, item_key);
create unique index if not exists ux_item_note_type_key on item_note(item_type, item_key);
create index if not exists ix_recent_view_viewed_at on recent_view(viewed_at desc);
create index if not exists ix_click_stat_rank on click_stat(item_type, click_count desc, last_clicked_at desc);
";
}
