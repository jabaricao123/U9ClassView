using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Http;
using U9.Subsidiary.Lib;

public class BaseApiController : ApiController
{
    private string _erpConnectionString;

    protected string ErpConnectionString
    {
        get
        {
            if (!string.IsNullOrEmpty(_erpConnectionString))
            {
                return _erpConnectionString;
            }

            _erpConnectionString = EnvConfig.GetErpConnectionString();
            if (!string.IsNullOrEmpty(_erpConnectionString))
            {
                return _erpConnectionString;
            }

            _erpConnectionString = GetSavedDefaultErpConnectionString();
            if (!string.IsNullOrEmpty(_erpConnectionString))
            {
                return _erpConnectionString;
            }

            var configConn = ConfigurationManager.ConnectionStrings["ConnectionString"];
            if (configConn != null && !string.IsNullOrWhiteSpace(configConn.ConnectionString))
            {
                _erpConnectionString = configConn.ConnectionString;
                return _erpConnectionString;
            }

            var context = HttpContext.Current;
            ConnectionModel model = null;
            if (context != null)
            {
                if (context.Session != null)
                {
                    model = context.Session["Connection"] as ConnectionModel;
                }

                if (model == null && context.Application != null)
                {
                    model = context.Application["Connection"] as ConnectionModel;
                }
            }

            if (model != null)
            {
                _erpConnectionString = model.ToString();
                return _erpConnectionString;
            }

            return string.Empty;
        }
    }

    protected DataTable ExecuteErpQuery(string sql, SqlParameter[] parameters = null)
    {
        var stopwatch = Stopwatch.StartNew();
        FileLogger.Info("ERP query start sql=" + sql);
        try
        {
            var ds = new DataSet();
            using (var conn = new SqlConnection(ErpConnectionString))
            using (var cmd = new SqlCommand(sql, conn))
            {
                if (parameters != null && parameters.Length > 0)
                {
                    cmd.Parameters.AddRange(parameters);
                }

                using (var da = new SqlDataAdapter(cmd))
                {
                    da.Fill(ds);
                }
            }

            var table = ds.Tables.Count == 0 ? new DataTable() : ds.Tables[0];
            FileLogger.Info("ERP query end rows=" + table.Rows.Count + " elapsedMs=" + stopwatch.ElapsedMilliseconds);
            return table;
        }
        catch (Exception ex)
        {
            FileLogger.Error("ERP query failed elapsedMs=" + stopwatch.ElapsedMilliseconds + " sql=" + sql, ex);
            throw;
        }
    }

    protected DataTable ExecuteMetaQuery(string sql, IDictionary<string, object> parameters = null)
    {
        var stopwatch = Stopwatch.StartNew();
        FileLogger.Info("META query start sql=" + sql);
        try
        {
            var table = SQLiteMetaStore.Query(sql, parameters);
            FileLogger.Info("META query end rows=" + table.Rows.Count + " elapsedMs=" + stopwatch.ElapsedMilliseconds);
            return table;
        }
        catch (Exception ex)
        {
            FileLogger.Error("META query failed elapsedMs=" + stopwatch.ElapsedMilliseconds + " sql=" + sql, ex);
            throw;
        }
    }

    protected object ExecuteMetaScalar(string sql, IDictionary<string, object> parameters = null)
    {
        var stopwatch = Stopwatch.StartNew();
        FileLogger.Info("META scalar start sql=" + sql);
        try
        {
            var result = SQLiteMetaStore.Scalar(sql, parameters);
            FileLogger.Info("META scalar end elapsedMs=" + stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            FileLogger.Error("META scalar failed elapsedMs=" + stopwatch.ElapsedMilliseconds + " sql=" + sql, ex);
            throw;
        }
    }

    protected int ExecuteMetaNonQuery(string sql, IDictionary<string, object> parameters = null)
    {
        var stopwatch = Stopwatch.StartNew();
        FileLogger.Info("META nonquery start sql=" + sql);
        try
        {
            var affected = SQLiteMetaStore.NonQuery(sql, parameters);
            FileLogger.Info("META nonquery end affected=" + affected + " elapsedMs=" + stopwatch.ElapsedMilliseconds);
            return affected;
        }
        catch (Exception ex)
        {
            FileLogger.Error("META nonquery failed elapsedMs=" + stopwatch.ElapsedMilliseconds + " sql=" + sql, ex);
            throw;
        }
    }

    protected bool TestErpConnection(string connectionString)
    {
        var stopwatch = Stopwatch.StartNew();
        FileLogger.Info("ERP connection test start connectionString=" + FileLogger.MaskSensitive(connectionString));
        try
        {
            using (var conn = new SqlConnection(connectionString))
            {
                conn.Open();
                FileLogger.Info("ERP connection test success elapsedMs=" + stopwatch.ElapsedMilliseconds);
                return true;
            }
        }
        catch (Exception ex)
        {
            FileLogger.Error("ERP connection test failed elapsedMs=" + stopwatch.ElapsedMilliseconds, ex);
            return false;
        }
    }

    protected HashSet<string> GetFavoritedKeys(string itemType)
    {
        DataTable table;
        try
        {
            table = ExecuteMetaQuery(
                "select item_key from favorite_item where item_type=@item_type",
                new Dictionary<string, object> { { "@item_type", itemType ?? string.Empty } });
        }
        catch (Exception ex)
        {
            FileLogger.Warn("Meta unavailable, skip favorites for " + itemType + ": " + ex.Message);
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return new HashSet<string>(
            table.Rows.Cast<DataRow>().Select(r => Convert.ToString(GetValue(r, "item_key")) ?? string.Empty),
            StringComparer.OrdinalIgnoreCase);
    }

    protected Dictionary<string, ClickInfo> GetClickStats(string itemType)
    {
        var result = new Dictionary<string, ClickInfo>(StringComparer.OrdinalIgnoreCase);
        DataTable table;
        try
        {
            table = ExecuteMetaQuery(
                "select item_key, click_count, last_clicked_at from click_stat where item_type=@item_type",
                new Dictionary<string, object> { { "@item_type", itemType ?? string.Empty } });
        }
        catch (Exception ex)
        {
            FileLogger.Warn("Meta unavailable, skip click stats for " + itemType + ": " + ex.Message);
            return result;
        }

        foreach (DataRow row in table.Rows)
        {
            var key = Convert.ToString(GetValue(row, "item_key")) ?? string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            result[key] = new ClickInfo
            {
                ClickCount = ConvertToInt(GetValue(row, "click_count")),
                LastClickedAt = ConvertToDateTime(GetValue(row, "last_clicked_at"))
            };
        }

        return result;
    }

    protected Dictionary<string, DateTime?> GetRecentViews(string itemType)
    {
        var result = new Dictionary<string, DateTime?>(StringComparer.OrdinalIgnoreCase);
        DataTable table;
        try
        {
            table = ExecuteMetaQuery(
                "select item_key, viewed_at from recent_view where item_type=@item_type",
                new Dictionary<string, object> { { "@item_type", itemType ?? string.Empty } });
        }
        catch (Exception ex)
        {
            FileLogger.Warn("Meta unavailable, skip recent views for " + itemType + ": " + ex.Message);
            return result;
        }

        foreach (DataRow row in table.Rows)
        {
            var key = Convert.ToString(GetValue(row, "item_key")) ?? string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            result[key] = ConvertToDateTime(GetValue(row, "viewed_at"));
        }

        return result;
    }

    protected Dictionary<string, string> GetNotes(string itemType)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        DataTable table;
        try
        {
            table = ExecuteMetaQuery(
                "select item_key, note from item_note where item_type=@item_type",
                new Dictionary<string, object> { { "@item_type", itemType ?? string.Empty } });
        }
        catch (Exception ex)
        {
            FileLogger.Warn("Meta unavailable, skip notes for " + itemType + ": " + ex.Message);
            return result;
        }

        foreach (DataRow row in table.Rows)
        {
            var key = Convert.ToString(GetValue(row, "item_key")) ?? string.Empty;
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            result[key] = Convert.ToString(GetValue(row, "note")) ?? string.Empty;
        }

        return result;
    }

    protected void RecordRecentView(string itemType, string itemKey, string title, string subtitle, string extraJson)
    {
        try
        {
            ExecuteMetaNonQuery(
                @"insert into recent_view(item_type, item_key, title, subtitle, extra_json, viewed_at)
                  values(@item_type, @item_key, @title, @subtitle, @extra_json, current_timestamp)
                  on conflict(item_type, item_key) do update set
                    title=excluded.title,
                    subtitle=excluded.subtitle,
                    extra_json=excluded.extra_json,
                    viewed_at=current_timestamp",
            new Dictionary<string, object>
            {
                { "@item_type", itemType ?? string.Empty },
                { "@item_key", itemKey ?? string.Empty },
                { "@title", title ?? string.Empty },
                { "@subtitle", subtitle ?? string.Empty },
                { "@extra_json", (object)extraJson ?? DBNull.Value }
            });
        }
        catch (Exception ex)
        {
            FileLogger.Warn("Meta unavailable, skip recent record for " + itemType + "/" + itemKey + ": " + ex.Message);
        }
    }

    protected void IncrementClick(string itemType, string itemKey)
    {
        try
        {
            ExecuteMetaNonQuery(
                @"insert into click_stat(item_type, item_key, click_count, last_clicked_at)
                  values(@item_type, @item_key, 1, current_timestamp)
                  on conflict(item_type, item_key) do update set
                    click_count=click_count + 1,
                    last_clicked_at=current_timestamp",
            new Dictionary<string, object>
            {
                { "@item_type", itemType ?? string.Empty },
                { "@item_key", itemKey ?? string.Empty }
            });
        }
        catch (Exception ex)
        {
            FileLogger.Warn("Meta unavailable, skip click record for " + itemType + "/" + itemKey + ": " + ex.Message);
        }
    }

    protected object GetValue(DataRow row, string columnName)
    {
        foreach (DataColumn column in row.Table.Columns)
        {
            if (string.Equals(column.ColumnName, columnName, StringComparison.OrdinalIgnoreCase))
            {
                return row[column];
            }
        }
        return null;
    }

    protected int ConvertToInt(object value)
    {
        if (value == null || value == DBNull.Value)
        {
            return 0;
        }

        int result;
        return int.TryParse(Convert.ToString(value), out result) ? result : 0;
    }

    protected DateTime? ConvertToDateTime(object value)
    {
        if (value == null || value == DBNull.Value)
        {
            return null;
        }

        DateTime dt;
        return DateTime.TryParse(Convert.ToString(value), out dt) ? dt : (DateTime?)null;
    }

    protected string EscapeLike(string input)
    {
        return (input ?? string.Empty).Replace("'", "''");
    }

    protected int GetMatchRank(string keyword, params string[] values)
    {
        var match = GetBestMatch(keyword, values);
        return match.Rank;
    }

    protected int GetMatchLength(string keyword, params string[] values)
    {
        var match = GetBestMatch(keyword, values);
        return match.Length;
    }

    private static MatchInfo GetBestMatch(string keyword, params string[] values)
    {
        var key = (keyword ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            return new MatchInfo { Rank = 99, Length = int.MaxValue };
        }

        var bestRank = 99;
        var bestLength = int.MaxValue;
        foreach (var value in values)
        {
            var text = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            int rank;
            if (string.Equals(text, key, StringComparison.OrdinalIgnoreCase))
            {
                rank = 0;
            }
            else if (text.StartsWith(key, StringComparison.OrdinalIgnoreCase))
            {
                rank = 1;
            }
            else if (text.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                rank = 2;
            }
            else
            {
                rank = 3;
            }

            if (rank < bestRank || (rank == bestRank && text.Length < bestLength))
            {
                bestRank = rank;
                bestLength = text.Length;
            }
        }

        return new MatchInfo { Rank = bestRank, Length = bestLength };
    }

    private string GetSavedDefaultErpConnectionString()
    {
        return string.Empty;
    }

    private static string NormalizeSqlServer(string server)
    {
        var value = (server ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (value.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return "tcp:" + value;
    }

    protected sealed class ClickInfo
    {
        public int ClickCount { get; set; }
        public DateTime? LastClickedAt { get; set; }
    }

    private sealed class MatchInfo
    {
        public int Rank { get; set; }
        public int Length { get; set; }
    }
}
