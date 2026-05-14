using System.Data;
using ClassView.Backend.Models;
using Microsoft.Data.SqlClient;

namespace ClassView.Backend.Services;

public sealed class ErpSqlService
{
    private readonly EnvConfig env;

    public ErpSqlService(EnvConfig env)
    {
        this.env = env;
    }

    public async Task<bool> TestAsync(DbConnectionProfile profile)
    {
        await using var conn = new SqlConnection(env.BuildConnectionString(profile));
        await conn.OpenAsync();
        return true;
    }

    public async Task<List<Dictionary<string, object?>>> QueryAsync(string sql, params SqlParameter[] parameters)
    {
        await using var conn = new SqlConnection(env.BuildConnectionString());
        await using var cmd = new SqlCommand(sql, conn);
        if (parameters.Length > 0)
        {
            cmd.Parameters.AddRange(parameters);
        }

        await conn.OpenAsync();
        await using var reader = await cmd.ExecuteReaderAsync();

        var rows = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
            }
            rows.Add(row);
        }

        return rows;
    }
}
