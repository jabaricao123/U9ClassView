using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Net.Http;
using System.Web.Http;

public class ConfigController : BaseApiController
{
    [HttpGet]
    public HttpResponseMessage Current()
    {
        try
        {
            var envProfile = EnvConfig.GetErpProfile();
            if (envProfile != null)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { success = true, data = envProfile });
            }

            var fallback = GetFallbackProfile();
            return Request.CreateResponse(HttpStatusCode.OK, new { success = true, data = fallback });
        }
        catch (Exception ex)
        {
            return Request.CreateResponse(HttpStatusCode.OK, new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public HttpResponseMessage Test([FromBody] DbConnectionProfile profile)
    {
        try
        {
            if (profile == null)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { success = false, message = "参数不能为空" });
            }

            var erpConnStr = string.Format(
                "User Id={0};Password={1};Data Source={2};Initial Catalog={3};packet size=4096;Max Pool size=1500;persist security info=True;Encrypt=True;TrustServerCertificate=True",
                profile.UserName,
                ResolveProfilePassword(profile),
                NormalizeServer(profile.Server),
                profile.DatabaseName);

            var ok = TestErpConnection(erpConnStr);

            return Request.CreateResponse(HttpStatusCode.OK, new
            {
                success = ok,
                message = ok ? "连接成功" : "连接失败"
            });
        }
        catch (Exception ex)
        {
            return Request.CreateResponse(HttpStatusCode.OK, new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public HttpResponseMessage Save([FromBody] DbConnectionProfile profile)
    {
        try
        {
            if (profile == null)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { success = false, message = "参数不能为空" });
            }

            var passwordToSave = ResolveProfilePassword(profile);
            EnvConfig.SaveErpProfile(profile, passwordToSave);
            return Request.CreateResponse(HttpStatusCode.OK, new { success = true });
        }
        catch (Exception ex)
        {
            return Request.CreateResponse(HttpStatusCode.OK, new { success = false, message = ex.Message });
        }
    }

    private static bool ParseBool(object value)
    {
        if (value == null || value == DBNull.Value)
        {
            return false;
        }

        var text = Convert.ToString(value);
        return string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "t", StringComparison.OrdinalIgnoreCase)
            || string.Equals(text, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeServer(string server)
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

    private DbConnectionProfile GetFallbackProfile()
    {
        var conn = ConfigurationManager.ConnectionStrings["ConnectionString"];
        if (conn == null || string.IsNullOrWhiteSpace(conn.ConnectionString))
        {
            return null;
        }

        var builder = new SqlConnectionStringBuilder(conn.ConnectionString);
        return new DbConnectionProfile
        {
            Id = 0,
            Name = "Web.Config",
            Server = builder.DataSource,
            DatabaseName = builder.InitialCatalog,
            UserName = builder.UserID,
            Password = string.Empty,
            IsDefault = true,
            HasPassword = !string.IsNullOrEmpty(builder.Password)
        };
    }

    private string ResolveProfilePassword(DbConnectionProfile profile)
    {
        if (profile == null)
        {
            return string.Empty;
        }

        if (!profile.KeepPassword)
        {
            return profile.Password ?? string.Empty;
        }

        var envPassword = EnvConfig.GetErpPassword();
        if (!string.IsNullOrEmpty(envPassword))
        {
            return envPassword;
        }

        var conn = ConfigurationManager.ConnectionStrings["ConnectionString"];
        if (conn == null || string.IsNullOrWhiteSpace(conn.ConnectionString))
        {
            return string.Empty;
        }

        return new SqlConnectionStringBuilder(conn.ConnectionString).Password;
    }
}

public class DbConnectionProfile
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Server { get; set; }
    public string DatabaseName { get; set; }
    public string UserName { get; set; }
    public string Password { get; set; }
    public bool IsDefault { get; set; }
    public bool HasPassword { get; set; }
    public bool KeepPassword { get; set; }
}
