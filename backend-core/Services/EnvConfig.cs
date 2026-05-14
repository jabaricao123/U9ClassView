using System.Text;
using ClassView.Backend.Models;
using Microsoft.Data.SqlClient;

namespace ClassView.Backend.Services;

public sealed class EnvConfig
{
    private readonly string envPath;

    public EnvConfig(IHostEnvironment env)
    {
        envPath = FindEnvPath(env.ContentRootPath);
    }

    public string EnvPath => envPath;

    public DbConnectionProfile? GetProfile()
    {
        var values = Load();
        values.TryGetValue("SQL_SERVER", out var server);
        values.TryGetValue("SQL_DATABASE", out var database);
        values.TryGetValue("SQL_USER", out var user);
        values.TryGetValue("SQL_PASSWORD", out var password);

        if (string.IsNullOrWhiteSpace(server) && string.IsNullOrWhiteSpace(database))
        {
            return null;
        }

        return new DbConnectionProfile
        {
            Server = server ?? "",
            DatabaseName = database ?? "",
            UserName = user ?? "",
            Password = "",
            HasPassword = !string.IsNullOrEmpty(password),
            IsDefault = true
        };
    }

    public string GetPassword()
    {
        var values = Load();
        return values.TryGetValue("SQL_PASSWORD", out var password) ? password : "";
    }

    public string BuildConnectionString(DbConnectionProfile? profile = null)
    {
        profile ??= GetProfile();
        if (profile is null)
        {
            return "";
        }

        var password = profile.KeepPassword ? GetPassword() : profile.Password;
        if (string.IsNullOrEmpty(password))
        {
            password = GetPassword();
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = NormalizeServer(profile.Server),
            InitialCatalog = profile.DatabaseName,
            UserID = profile.UserName,
            Password = password,
            Encrypt = true,
            TrustServerCertificate = true,
            PersistSecurityInfo = true,
            PacketSize = 4096,
            MaxPoolSize = 1500
        };
        return builder.ConnectionString;
    }

    public void SaveProfile(DbConnectionProfile profile)
    {
        var values = Load();
        var password = profile.KeepPassword ? GetPassword() : profile.Password;

        values["SQL_SERVER"] = profile.Server ?? "";
        values["SQL_DATABASE"] = profile.DatabaseName ?? "";
        values["SQL_USER"] = profile.UserName ?? "";
        values["SQL_PASSWORD"] = password ?? "";

        var lines = values.Select(pair => pair.Key + "=" + Quote(pair.Value));
        File.WriteAllText(envPath, string.Join(Environment.NewLine, lines) + Environment.NewLine, new UTF8Encoding(false));
    }

    private Dictionary<string, string> Load()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!File.Exists(envPath))
        {
            return values;
        }

        foreach (var line in File.ReadAllLines(envPath))
        {
            var text = line.Trim();
            if (text.Length == 0 || text.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var index = text.IndexOf('=');
            if (index <= 0)
            {
                continue;
            }

            values[text[..index].Trim()] = Unquote(text[(index + 1)..].Trim());
        }

        return values;
    }

    private static string FindEnvPath(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }

        return Path.Combine(start, ".env");
    }

    private static string NormalizeServer(string server)
    {
        var value = (server ?? "").Trim();
        if (value.Length == 0 || value.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return "tcp:" + value;
    }

    private static string Quote(string value)
    {
        return "\"" + (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1].Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        return value;
    }
}
