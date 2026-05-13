using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

public class EntityController : BaseApiController
{
    [HttpGet]
    public HttpResponseMessage Search(string keyword = "", bool fuzzy = true)
    {
        try
        {
            var whereClause = string.Empty;
            var safeKeyword = EscapeLike(keyword);
            if (!string.IsNullOrWhiteSpace(safeKeyword))
            {
                if (fuzzy)
                {
                    whereClause = string.Format(" and (a.Name like '%{0}%' or b.DisplayName like '%{0}%') ", safeKeyword);
                }
                else
                {
                    whereClause = string.Format(" and (a.Name ='{0}' or b.DisplayName ='{0}') ", safeKeyword);
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
+ @" order by Name";

            var table = ExecuteErpQuery(sql);
            var favorites = GetFavoritedKeys("entity");
            var clicks = GetClickStats("entity");
            var recents = GetRecentViews("entity");
            var notes = GetNotes("entity");

            var result = table.Rows.Cast<DataRow>().Select(row =>
            {
                var itemKey = Convert.ToString(GetValue(row, "ID")) ?? string.Empty;
                ClickInfo click;
                clicks.TryGetValue(itemKey, out click);
                DateTime? viewedAt;
                recents.TryGetValue(itemKey, out viewedAt);
                string note;
                notes.TryGetValue(itemKey, out note);

                return new
                {
                    FullName = Convert.ToString(GetValue(row, "FullName")),
                    Name = Convert.ToString(GetValue(row, "Name")),
                    DisplayName = Convert.ToString(GetValue(row, "DisplayName")),
                    DefaultTableName = Convert.ToString(GetValue(row, "DefaultTableName")),
                    ClassType = Convert.ToString(GetValue(row, "ClassType")),
                    ID = itemKey,
                    AssemblyName = Convert.ToString(GetValue(row, "AssemblyName")),
                    ItemType = "entity",
                    ItemKey = itemKey,
                    IsFavorite = favorites.Contains(itemKey),
                    Note = note ?? string.Empty,
                    ClickCount = click == null ? 0 : click.ClickCount,
                    LastClickedAt = click == null ? (DateTime?)null : click.LastClickedAt,
                    LastViewedAt = viewedAt,
                    MatchRank = GetMatchRank(safeKeyword,
                        Convert.ToString(GetValue(row, "DisplayName")),
                        Convert.ToString(GetValue(row, "Name")),
                        Convert.ToString(GetValue(row, "FullName")),
                        Convert.ToString(GetValue(row, "DefaultTableName"))),
                    MatchLength = GetMatchLength(safeKeyword,
                        Convert.ToString(GetValue(row, "DisplayName")),
                        Convert.ToString(GetValue(row, "Name")),
                        Convert.ToString(GetValue(row, "FullName")),
                        Convert.ToString(GetValue(row, "DefaultTableName")))
                };
            })
            .OrderBy(x => x.MatchRank)
            .ThenBy(x => x.MatchLength)
            .ThenByDescending(x => x.IsFavorite)
            .ThenByDescending(x => x.ClickCount)
            .ThenByDescending(x => x.LastViewedAt ?? DateTime.MinValue)
            .ThenBy(x => x.Name)
            .ToList();

            return Request.CreateResponse(HttpStatusCode.OK, new
            {
                success = true,
                data = result,
                total = result.Count
            });
        }
        catch (Exception ex)
        {
            return Request.CreateResponse(HttpStatusCode.OK, new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public HttpResponseMessage Attributes(string id)
    {
        try
        {
            var safeId = EscapeLike(id);
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
where MD_Class_ID='" + safeId + @"'
order by a.IsSystem desc, a.GroupName asc, a.[Name] asc";

            var table = ExecuteErpQuery(sql);
            var favorites = GetFavoritedKeys("entity_attr");
            var notes = GetNotes("entity_attr");
            var result = table.Rows.Cast<DataRow>().Select(row => new
            {
                Name = Convert.ToString(GetValue(row, "Name")),
                ID = Convert.ToString(GetValue(row, "ID")),
                FullName = Convert.ToString(GetValue(row, "FullName")),
                DefaultValue = Convert.ToString(GetValue(row, "DefaultValue")),
                IsCollection = Convert.ToString(GetValue(row, "IsCollection")),
                DisplayName = Convert.ToString(GetValue(row, "DisplayName")),
                Description = Convert.ToString(GetValue(row, "Description")),
                ClassType = Convert.ToString(GetValue(row, "ClassType")),
                IsKey = Convert.ToString(GetValue(row, "IsKey")),
                IsNullable = Convert.ToString(GetValue(row, "IsNullable")),
                IsReadOnly = Convert.ToString(GetValue(row, "IsReadOnly")),
                IsSystem = Convert.ToString(GetValue(row, "IsSystem")),
                IsBusinessKey = Convert.ToString(GetValue(row, "IsBusinessKey")),
                GroupName = Convert.ToString(GetValue(row, "GroupName")),
                ItemType = "entity_attr",
                ItemKey = safeId + "|" + (Convert.ToString(GetValue(row, "Name")) ?? string.Empty),
                IsFavorite = favorites.Contains(safeId + "|" + (Convert.ToString(GetValue(row, "Name")) ?? string.Empty)),
                Note = GetNote(notes, safeId + "|" + (Convert.ToString(GetValue(row, "Name")) ?? string.Empty))
            }).ToList();

            return Request.CreateResponse(HttpStatusCode.OK, new
            {
                success = true,
                data = result,
                total = result.Count
            });
        }
        catch (Exception ex)
        {
            return Request.CreateResponse(HttpStatusCode.OK, new { success = false, message = ex.Message });
        }
    }

    private static string GetNote(Dictionary<string, string> notes, string key)
    {
        string note;
        return notes.TryGetValue(key, out note) ? note : string.Empty;
    }
}
