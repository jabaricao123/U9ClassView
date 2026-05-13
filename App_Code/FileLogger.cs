using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

public static class FileLogger
{
    private static readonly object SyncRoot = new object();

    public static void Info(string message)
    {
        Write("INFO", message, null);
    }

    public static void Warn(string message)
    {
        Write("WARN", message, null);
    }

    public static void Error(string message)
    {
        Write("ERROR", message, null);
    }

    public static void Error(string message, Exception ex)
    {
        Write("ERROR", message, ex);
    }

    public static string MaskSensitive(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return Regex.Replace(
            text,
            "(Password|Pwd)\\s*=\\s*([^;]+)",
            "$1=***",
            RegexOptions.IgnoreCase);
    }

    private static void Write(string level, string message, Exception ex)
    {
        try
        {
            var dir = ResolveLogDirectory();
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var file = Path.Combine(dir, "classview-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
            var line = BuildLine(level, message, ex);

            lock (SyncRoot)
            {
                File.AppendAllText(file, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never break a business request.
        }
    }

    private static string ResolveLogDirectory()
    {
        var context = HttpContext.Current;
        if (context != null && context.Server != null)
        {
            return context.Server.MapPath("~/App_Data/Logs");
        }

        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Logs");
    }

    private static string BuildLine(string level, string message, Exception ex)
    {
        var builder = new StringBuilder();
        builder.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        builder.Append(" [");
        builder.Append(level);
        builder.Append("] ");
        builder.Append(MaskSensitive(message));

        var context = HttpContext.Current;
        if (context != null && context.Request != null)
        {
            builder.Append(" | ");
            builder.Append(context.Request.HttpMethod);
            builder.Append(" ");
            builder.Append(context.Request.RawUrl);
            builder.Append(" | IP=");
            builder.Append(context.Request.UserHostAddress);
        }

        if (ex != null)
        {
            builder.AppendLine();
            builder.Append(MaskSensitive(ex.ToString()));
        }

        builder.AppendLine();
        return builder.ToString();
    }
}
