using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

public class ViewController : BaseApiController
{
    [HttpGet]
    public HttpResponseMessage Search(string keyword = "", bool fuzzy = true)
    {
        try
        {
            var safeKeyword = EscapeLike(keyword);
            var whereClause = string.Empty;
            if (!string.IsNullOrWhiteSpace(safeKeyword))
            {
                if (fuzzy)
                {
                    whereClause = string.Format(
                        " where f.ClassName like '%{0}%' OR f.AssemblyName like '%{0}%' OR ft.DisplayName like '%{0}%' OR f.Name like '%{0}%' OR m.Name like '%{0}%' ",
                        safeKeyword);
                }
                else
                {
                    whereClause = string.Format(
                        " where f.ClassName='{0}' OR f.AssemblyName='{0}' OR ft.DisplayName='{0}' OR f.Name='{0}' OR m.Name ='{0}' ",
                        safeKeyword);
                }
            }

            var sql = @"
select top 500
    v.FilterOriginalOPath as FilterOriginalOPath,
    f.URI as Uri,
    f.Width as Width,
    f.Height as Height,
    v.Name as ViewName,
    vt.DisplayName as ViewDisplayName,
    m.Name as UIModel,
    f.Name as UIForm,
    ft.DisplayName as FormDisplayName,
    f.AssemblyName as AssemblyName,
    f.ClassName as ClassName
from UBF_MD_UIView v
left join UBF_MD_UIView_Trl vt on v.ID=vt.ID
left join UBF_MD_UIModel m on m.ID=v.UIModel
left join UBF_MD_UIForm f on m.UIComponent=f.UIComponent
left join UBF_MD_UIForm_Trl ft on f.ID=ft.ID"
            + whereClause
            + " order by f.ClassName desc";

            var table = ExecuteErpQuery(sql);
            var favorites = GetFavoritedKeys("view");
            var clicks = GetClickStats("view");
            var recents = GetRecentViews("view");
            var notes = GetNotes("view");

            var result = table.Rows.Cast<DataRow>().Select(row =>
            {
                var className = Convert.ToString(GetValue(row, "ClassName")) ?? string.Empty;
                var viewName = Convert.ToString(GetValue(row, "ViewName")) ?? string.Empty;
                var itemKey = className + "|" + viewName;

                ClickInfo click;
                clicks.TryGetValue(itemKey, out click);
                DateTime? viewedAt;
                recents.TryGetValue(itemKey, out viewedAt);
                string note;
                notes.TryGetValue(itemKey, out note);

                return new
                {
                    FilterOriginalOPath = Convert.ToString(GetValue(row, "FilterOriginalOPath")),
                    Uri = Convert.ToString(GetValue(row, "Uri")),
                    Width = Convert.ToString(GetValue(row, "Width")),
                    Height = Convert.ToString(GetValue(row, "Height")),
                    ViewName = viewName,
                    ViewDisplayName = Convert.ToString(GetValue(row, "ViewDisplayName")),
                    UIModel = Convert.ToString(GetValue(row, "UIModel")),
                    UIForm = Convert.ToString(GetValue(row, "UIForm")),
                    FormDisplayName = Convert.ToString(GetValue(row, "FormDisplayName")),
                    AssemblyName = Convert.ToString(GetValue(row, "AssemblyName")),
                    ClassName = className,
                    ItemType = "view",
                    ItemKey = itemKey,
                    IsFavorite = favorites.Contains(itemKey),
                    Note = note ?? string.Empty,
                    ClickCount = click == null ? 0 : click.ClickCount,
                    LastClickedAt = click == null ? (DateTime?)null : click.LastClickedAt,
                    LastViewedAt = viewedAt,
                    MatchRank = GetMatchRank(safeKeyword,
                        Convert.ToString(GetValue(row, "ViewDisplayName")),
                        viewName,
                        Convert.ToString(GetValue(row, "UIForm")),
                        Convert.ToString(GetValue(row, "FormDisplayName")),
                        className,
                        Convert.ToString(GetValue(row, "AssemblyName"))),
                    MatchLength = GetMatchLength(safeKeyword,
                        Convert.ToString(GetValue(row, "ViewDisplayName")),
                        viewName,
                        Convert.ToString(GetValue(row, "UIForm")),
                        Convert.ToString(GetValue(row, "FormDisplayName")),
                        className,
                        Convert.ToString(GetValue(row, "AssemblyName")))
                };
            })
            .OrderBy(x => x.MatchRank)
            .ThenBy(x => x.MatchLength)
            .ThenByDescending(x => x.IsFavorite)
            .ThenByDescending(x => x.ClickCount)
            .ThenByDescending(x => x.LastViewedAt ?? DateTime.MinValue)
            .ThenBy(x => x.ClassName)
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
    public HttpResponseMessage Fields(string className = "", string viewName = "")
    {
        try
        {
            var safeClassName = EscapeLike(className);
            var safeViewName = EscapeLike(viewName);
            var sql = @"
select
    d.[Name] as Name,
    d.[ToolTips] as ToolTips,
    d.[DataType] as DataType,
    d.[DefaultValue] as DefaultValue,
    d.[GroupName] as GroupName,
    f.ClassName as ClassName,
    v.Name as ViewName
from UBF_MD_UIField d
left join UBF_MD_UIView v on d.UIView=v.ID
left join UBF_MD_UIModel m on m.ID=v.UIModel
left join UBF_MD_UIForm f on m.UIComponent=f.UIComponent
where f.ClassName ='" + safeClassName + "' and v.Name='" + safeViewName + @"'
order by GroupName asc";

            var table = ExecuteErpQuery(sql);
            var fieldItemType = "view_field";
            var favorites = GetFavoritedKeys(fieldItemType);
            var notes = GetNotes(fieldItemType);
            var result = table.Rows.Cast<DataRow>().Select(row => new
            {
                Name = Convert.ToString(GetValue(row, "Name")),
                ToolTips = Convert.ToString(GetValue(row, "ToolTips")),
                DataType = Convert.ToString(GetValue(row, "DataType")),
                DefaultValue = Convert.ToString(GetValue(row, "DefaultValue")),
                GroupName = Convert.ToString(GetValue(row, "GroupName")),
                ClassName = Convert.ToString(GetValue(row, "ClassName")),
                ViewName = Convert.ToString(GetValue(row, "ViewName")),
                ItemType = fieldItemType,
                ItemKey = safeClassName + "|" + safeViewName + "|" + (Convert.ToString(GetValue(row, "Name")) ?? string.Empty),
                IsFavorite = favorites.Contains(safeClassName + "|" + safeViewName + "|" + (Convert.ToString(GetValue(row, "Name")) ?? string.Empty)),
                Note = GetNote(notes, safeClassName + "|" + safeViewName + "|" + (Convert.ToString(GetValue(row, "Name")) ?? string.Empty))
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
