using ClassView.Backend.Models;
using ClassView.Backend.Services;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = null;
});
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});
builder.Services.AddSingleton<EnvConfig>();
builder.Services.AddSingleton<ErpSqlService>();
builder.Services.AddSingleton<MetaStore>();

var app = builder.Build();
app.UseCors();

var repoRoot = Directory.GetParent(app.Environment.ContentRootPath)?.FullName ?? app.Environment.ContentRootPath;
var frontendPath = Path.Combine(repoRoot, "frontend");
if (Directory.Exists(frontendPath))
{
    var provider = new PhysicalFileProvider(frontendPath);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = provider });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = provider,
        ContentTypeProvider = new FileExtensionContentTypeProvider()
    });
}

app.MapGet("/api/health", () => new { success = true, data = new { service = "backend-core", time = DateTimeOffset.Now } });

app.MapGet("/api/config/current", (EnvConfig env) =>
{
    var profile = env.GetProfile();
    return Ok(profile);
});

app.MapPost("/api/config/test", async (DbConnectionProfile profile, ErpSqlService erp) =>
{
    try
    {
        var ok = await erp.TestAsync(profile);
        return Results.Json(new { success = ok, message = ok ? "连接成功" : "连接失败" });
    }
    catch (Exception ex)
    {
        return Fail(ex.Message);
    }
});

app.MapPost("/api/config/save", (DbConnectionProfile profile, EnvConfig env) =>
{
    try
    {
        env.SaveProfile(profile);
        return Ok();
    }
    catch (Exception ex)
    {
        return Fail(ex.Message);
    }
});

app.MapGet("/api/entity/search", async (string? keyword, bool fuzzy, ErpSqlService erp, MetaStore meta) =>
{
    try
    {
        var key = (keyword ?? "").Trim();
        var whereClause = "";
        var parameters = new List<SqlParameter>();
        if (key.Length > 0)
        {
            if (fuzzy)
            {
                whereClause = " and (a.Name like @keyword or b.DisplayName like @keyword) ";
                parameters.Add(new SqlParameter("@keyword", "%" + key + "%"));
            }
            else
            {
                whereClause = " and (a.Name=@keyword or b.DisplayName=@keyword) ";
                parameters.Add(new SqlParameter("@keyword", key));
            }
        }

        var sql = @"
SELECT
    a.[FullName] as FullName,
    a.[Name] as Name,
    b.[DisplayName] as DisplayName,
    a.DefaultTableName as DefaultTableName,
    a.[ClassType] as ClassType,
    a.[ID] as ID,
    c.AssemblyName as AssemblyName
FROM [UBF_MD_Class] as a
left join UBF_MD_Class_trl as b on a.Local_ID=b.Local_ID
left join UBF_MD_Component as c on a.MD_Component_ID=c.ID
where ClassType in(1,2,3)
  and (b.sysmlflag='zh-CN' or b.sysmlflag is null)"
+ whereClause
+ " order by Name";

        var rows = await erp.QueryAsync(sql, parameters.ToArray());
        var favoritesTask = meta.GetFavoritedKeysAsync("entity");
        var clicksTask = meta.GetClickStatsAsync("entity");
        var recentsTask = meta.GetRecentViewsAsync("entity");
        var notesTask = meta.GetNotesAsync("entity");
        await Task.WhenAll(favoritesTask, clicksTask, recentsTask, notesTask);

        var favorites = favoritesTask.Result;
        var clicks = clicksTask.Result;
        var recents = recentsTask.Result;
        var notes = notesTask.Result;

        var result = rows.Select(row =>
        {
            var itemKey = S(row, "ID");
            clicks.TryGetValue(itemKey, out var click);
            recents.TryGetValue(itemKey, out var viewedAt);
            notes.TryGetValue(itemKey, out var note);

            return new EntitySearchRow
            {
                FullName = S(row, "FullName"),
                Name = S(row, "Name"),
                DisplayName = S(row, "DisplayName"),
                DefaultTableName = S(row, "DefaultTableName"),
                ClassType = S(row, "ClassType"),
                ID = itemKey,
                AssemblyName = S(row, "AssemblyName"),
                ItemType = "entity",
                ItemKey = itemKey,
                IsFavorite = favorites.Contains(itemKey),
                Note = note ?? "",
                ClickCount = click?.ClickCount ?? 0,
                LastClickedAt = click?.LastClickedAt,
                LastViewedAt = viewedAt,
                MatchRank = GetMatchRank(key, S(row, "DisplayName"), S(row, "Name"), S(row, "FullName"), S(row, "DefaultTableName")),
                MatchLength = GetMatchLength(key, S(row, "DisplayName"), S(row, "Name"), S(row, "FullName"), S(row, "DefaultTableName"))
            };
        })
        .OrderBy(x => x.MatchRank)
        .ThenBy(x => x.MatchLength)
        .ThenByDescending(x => x.IsFavorite)
        .ThenByDescending(x => x.ClickCount)
        .ThenByDescending(x => x.LastViewedAt ?? DateTime.MinValue)
        .ThenBy(x => x.Name)
        .ToList();

        return Ok(result, total: result.Count);
    }
    catch (Exception ex)
    {
        return Fail(ex.Message);
    }
});

app.MapGet("/api/entity/attributes", async (string id, ErpSqlService erp, MetaStore meta) =>
{
    try
    {
        var sql = @"
SELECT
    a.[Name] as Name,
    a.DataTypeID as ID,
    b.FullName as FullName,
    a.DefaultValue as DefaultValue,
    a.IsCollection as IsCollection,
    c.DisplayName as DisplayName,
    c.[Description] as Description,
    b.ClassType as ClassType,
    a.IsKey as IsKey,
    a.IsNullable as IsNullable,
    a.IsReadOnly as IsReadOnly,
    a.IsSystem as IsSystem,
    a.IsBusinessKey as IsBusinessKey,
    a.GroupName as GroupName
from UBF_MD_Attribute a
inner join UBF_MD_CLASS b ON a.DataTypeID = b.ID
left join UBF_MD_Attribute_trl as c on a.Local_ID=c.Local_ID
where MD_Class_ID=@id
order by a.IsSystem desc, a.GroupName asc, a.[Name] asc";

        var rows = await erp.QueryAsync(sql, new SqlParameter("@id", id));
        var favoritesTask = meta.GetFavoritedKeysAsync("entity_attr");
        var notesTask = meta.GetNotesAsync("entity_attr");
        await Task.WhenAll(favoritesTask, notesTask);

        var favorites = favoritesTask.Result;
        var notes = notesTask.Result;
        var result = rows.Select(row =>
        {
            var itemKey = id + "|" + S(row, "Name");
            notes.TryGetValue(itemKey, out var note);
            return new
            {
                Name = S(row, "Name"),
                ID = S(row, "ID"),
                FullName = S(row, "FullName"),
                DefaultValue = S(row, "DefaultValue"),
                IsCollection = S(row, "IsCollection"),
                DisplayName = S(row, "DisplayName"),
                Description = S(row, "Description"),
                ClassType = S(row, "ClassType"),
                IsKey = S(row, "IsKey"),
                IsNullable = S(row, "IsNullable"),
                IsReadOnly = S(row, "IsReadOnly"),
                IsSystem = S(row, "IsSystem"),
                IsBusinessKey = S(row, "IsBusinessKey"),
                GroupName = S(row, "GroupName"),
                ItemType = "entity_attr",
                ItemKey = itemKey,
                IsFavorite = favorites.Contains(itemKey),
                Note = note ?? ""
            };
        }).ToList();

        return Ok(result, total: result.Count);
    }
    catch (Exception ex)
    {
        return Fail(ex.Message);
    }
});

app.MapGet("/api/favorite/list", async (string? itemType, MetaStore meta) =>
{
    var rows = await meta.ListFavoritesAsync(itemType);
    return Ok(rows, total: rows.Count);
});

app.MapPost("/api/favorite/toggle", async (FavoriteToggleRequest request, MetaStore meta) =>
{
    if (string.IsNullOrWhiteSpace(request.ItemType) || string.IsNullOrWhiteSpace(request.ItemKey))
    {
        return Fail("参数不能为空");
    }

    var isFavorited = await meta.ToggleFavoriteAsync(request);
    return Ok(new { isFavorited });
});

app.MapGet("/api/note/list", async (string? itemType, MetaStore meta) =>
{
    var notes = await meta.GetNotesAsync(itemType ?? "");
    var rows = notes.Select(x => new { ItemType = itemType ?? "", ItemKey = x.Key, Note = x.Value }).ToList();
    return Ok(rows, total: rows.Count);
});

app.MapPost("/api/note/save", async (NoteSaveRequest request, MetaStore meta) =>
{
    if (string.IsNullOrWhiteSpace(request.ItemType) || string.IsNullOrWhiteSpace(request.ItemKey))
    {
        return Fail("参数不能为空");
    }

    await meta.SaveNoteAsync(request);
    return Ok();
});

app.MapGet("/api/recent/list", async (string? itemType, int top, MetaStore meta) =>
{
    var rows = await meta.ListRecentAsync(itemType, top);
    return Ok(rows, total: rows.Count);
});

app.MapPost("/api/recent/record", async (RecentRecordRequest request, MetaStore meta) =>
{
    if (string.IsNullOrWhiteSpace(request.ItemType) || string.IsNullOrWhiteSpace(request.ItemKey))
    {
        return Fail("参数不能为空");
    }

    await meta.RecordRecentAsync(request);
    return Ok();
});

app.MapPost("/api/recent/click", async (RecentClickRequest request, MetaStore meta) =>
{
    if (string.IsNullOrWhiteSpace(request.ItemType) || string.IsNullOrWhiteSpace(request.ItemKey))
    {
        return Fail("参数不能为空");
    }

    await meta.IncrementClickAsync(request);
    return Ok();
});

app.Run();

static IResult Ok(object? data = null, int? total = null)
{
    var response = total.HasValue
        ? new { success = true, data, total = total.Value }
        : new { success = true, data };
    return Results.Json(response);
}

static IResult Fail(string message)
{
    return Results.Json(new { success = false, message });
}

static string S(IReadOnlyDictionary<string, object?> row, string key)
{
    return row.TryGetValue(key, out var value) ? Convert.ToString(value) ?? "" : "";
}

static int GetMatchRank(string keyword, params string[] values)
{
    return GetBestMatch(keyword, values).Rank;
}

static int GetMatchLength(string keyword, params string[] values)
{
    return GetBestMatch(keyword, values).Length;
}

static (int Rank, int Length) GetBestMatch(string keyword, params string[] values)
{
    var key = (keyword ?? "").Trim();
    if (key.Length == 0)
    {
        return (99, int.MaxValue);
    }

    var bestRank = 99;
    var bestLength = int.MaxValue;
    foreach (var value in values.Select(x => (x ?? "").Trim()).Where(x => x.Length > 0))
    {
        var rank = string.Equals(value, key, StringComparison.OrdinalIgnoreCase) ? 0
            : value.StartsWith(key, StringComparison.OrdinalIgnoreCase) ? 1
            : value.Contains(key, StringComparison.OrdinalIgnoreCase) ? 2
            : 3;

        if (rank < bestRank || rank == bestRank && value.Length < bestLength)
        {
            bestRank = rank;
            bestLength = value.Length;
        }
    }

    return (bestRank, bestLength);
}

internal sealed class EntitySearchRow
{
    public string FullName { get; set; } = "";
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string DefaultTableName { get; set; } = "";
    public string ClassType { get; set; } = "";
    public string ID { get; set; } = "";
    public string AssemblyName { get; set; } = "";
    public string ItemType { get; set; } = "";
    public string ItemKey { get; set; } = "";
    public bool IsFavorite { get; set; }
    public string Note { get; set; } = "";
    public int ClickCount { get; set; }
    public DateTime? LastClickedAt { get; set; }
    public DateTime? LastViewedAt { get; set; }
    public int MatchRank { get; set; }
    public int MatchLength { get; set; }
}
