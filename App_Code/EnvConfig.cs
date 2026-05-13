using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Web;

public static class EnvConfig
{
    private const string ErpPrefix = "SQL";

    public static string EnvPath
    {
        get
        {
            var context = HttpContext.Current;
            if (context != null)
            {
                return context.Server.MapPath("~/.env");
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");
        }
    }

    public static Dictionary<string, string> Load()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var path = EnvPath;
        if (!File.Exists(path))
        {
            return values;
        }

        foreach (var line in File.ReadAllLines(path))
        {
            var text = (line ?? string.Empty).Trim();
            if (text.Length == 0 || text.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var index = text.IndexOf('=');
            if (index <= 0)
            {
                continue;
            }

            var key = text.Substring(0, index).Trim();
            var value = text.Substring(index + 1).Trim();
            values[key] = Unquote(value);
        }

        return values;
    }

    public static string GetErpConnectionString()
    {
        return BuildConnectionString(Load(), ErpPrefix);
    }

    public static DbConnectionProfile GetErpProfile()
    {
        return GetProfile(Load(), ErpPrefix, ".env");
    }

    public static string GetErpPassword()
    {
        var values = Load();
        string password;
        return values.TryGetValue(ErpPrefix + "_PASSWORD", out password) ? password : string.Empty;
    }

    public static void SaveErpProfile(DbConnectionProfile profile, string password)
    {
        var values = Load();
        SetProfile(values, ErpPrefix, profile, password);
        Save(values);
    }

    public static DbConnectionProfile GetProfile(Dictionary<string, string> values, string prefix, string name)
    {
        string server;
        string database;
        string user;
        string password;
        values.TryGetValue(prefix + "_SERVER", out server);
        values.TryGetValue(prefix + "_DATABASE", out database);
        values.TryGetValue(prefix + "_USER", out user);
        values.TryGetValue(prefix + "_PASSWORD", out password);

        if (string.IsNullOrWhiteSpace(server) && string.IsNullOrWhiteSpace(database))
        {
            return null;
        }

        return new DbConnectionProfile
        {
            Id = 0,
            Name = name,
            Server = server ?? string.Empty,
            DatabaseName = database ?? string.Empty,
            UserName = user ?? string.Empty,
            Password = string.Empty,
            IsDefault = true,
            HasPassword = !string.IsNullOrEmpty(password)
        };
    }

    private static string BuildConnectionString(Dictionary<string, string> values, string prefix)
    {
        string server;
        string database;
        string user;
        string password;
        values.TryGetValue(prefix + "_SERVER", out server);
        values.TryGetValue(prefix + "_DATABASE", out database);
        values.TryGetValue(prefix + "_USER", out user);
        values.TryGetValue(prefix + "_PASSWORD", out password);

        if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database))
        {
            return string.Empty;
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            PersistSecurityInfo = true,
            Encrypt = true,
            TrustServerCertificate = true,
            PacketSize = 4096,
            MaxPoolSize = 1500
        };

        if (string.IsNullOrEmpty(user) && string.IsNullOrEmpty(password))
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = user ?? string.Empty;
            builder.Password = password ?? string.Empty;
        }

        return builder.ConnectionString;
    }

    private static void SetProfile(Dictionary<string, string> values, string prefix, DbConnectionProfile profile, string password)
    {
        values[prefix + "_SERVER"] = profile.Server ?? string.Empty;
        values[prefix + "_DATABASE"] = profile.DatabaseName ?? string.Empty;
        values[prefix + "_USER"] = profile.UserName ?? string.Empty;
        values[prefix + "_PASSWORD"] = password ?? string.Empty;
    }

    private static void Save(Dictionary<string, string> values)
    {
        var lines = new List<string>();
        foreach (var pair in values)
        {
            lines.Add(pair.Key + "=" + Quote(pair.Value));
        }

        File.WriteAllText(EnvPath, string.Join(Environment.NewLine, lines) + Environment.NewLine, new UTF8Encoding(false));
    }

    private static string Quote(string value)
    {
        value = value ?? string.Empty;
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
        {
            value = value.Substring(1, value.Length - 2);
            return value.Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        return value;
    }
}
