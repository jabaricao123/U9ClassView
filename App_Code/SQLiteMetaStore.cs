using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Web;

public static class SQLiteMetaStore
{
    private static readonly object InitLock = new object();
    private static bool initialized;

    private static string DbPath
    {
        get
        {
            var context = HttpContext.Current;
            var appData = context != null
                ? context.Server.MapPath("~/App_Data")
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data");
            return Path.Combine(appData, "classview-meta.sqlite");
        }
    }

    public static DataTable Query(string sql, IDictionary<string, object> parameters)
    {
        EnsureInitialized();
        using (var conn = CreateConnection())
        using (var cmd = CreateCommand(conn, sql, parameters))
        using (var adapter = new SQLiteDataAdapter(cmd))
        {
            var table = new DataTable();
            adapter.Fill(table);
            return table;
        }
    }

    public static object Scalar(string sql, IDictionary<string, object> parameters)
    {
        EnsureInitialized();
        using (var conn = CreateConnection())
        using (var cmd = CreateCommand(conn, sql, parameters))
        {
            conn.Open();
            return cmd.ExecuteScalar();
        }
    }

    public static int NonQuery(string sql, IDictionary<string, object> parameters)
    {
        EnsureInitialized();
        using (var conn = CreateConnection())
        using (var cmd = CreateCommand(conn, sql, parameters))
        {
            conn.Open();
            return cmd.ExecuteNonQuery();
        }
    }

    private static SQLiteConnection CreateConnection()
    {
        return new SQLiteConnection("Data Source=" + DbPath + ";Version=3;Foreign Keys=True;");
    }

    private static SQLiteCommand CreateCommand(SQLiteConnection conn, string sql, IDictionary<string, object> parameters)
    {
        var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (parameters == null)
        {
            return cmd;
        }

        foreach (var pair in parameters)
        {
            cmd.Parameters.AddWithValue(pair.Key, pair.Value ?? DBNull.Value);
        }

        return cmd;
    }

    private static void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        lock (InitLock)
        {
            if (initialized)
            {
                return;
            }

            var path = DbPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (!File.Exists(path))
            {
                SQLiteConnection.CreateFile(path);
            }

            using (var conn = CreateConnection())
            {
                conn.Open();
                ExecuteSchema(conn);
            }

            initialized = true;
        }
    }

    private static void ExecuteSchema(SQLiteConnection conn)
    {
        var sql = @"
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
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }
}
