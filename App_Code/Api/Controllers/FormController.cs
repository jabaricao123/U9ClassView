using System;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

public class FormController : BaseApiController
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
                        " where page.URI like '%{0}%' OR page.Name like '%{0}%' OR menu.Name like '%{0}%' OR part.Component='{0}' ",
                        safeKeyword);
                }
                else
                {
                    whereClause = string.Format(
                        " where page.URI ='{0}' OR page.Name ='{0}' OR menu.Name ='{0}' OR part.Component='{0}' ",
                        safeKeyword);
                }
            }

            var sql = @"
select
    uiForm.AssemblyName as AssemblyName,
    uiForm.ClassName as ClassName,
    uiForm.UID as FormID,
    page.Name as Name,
    page.URI as Url,
    page.Application as Application,
    pmenu.Name+'->'+menu.Name as MenuName
from UBF_MD_UIForm as uiForm
left join UBF_Assemble_Part as part on part.Component=uiForm.[UID]
left join UBF_Assemble_ColumnPart as columnPart on columnPart.Part=part.ID
left join UBF_Assemble_PageColumn as pageColumn on columnPart.PageColumn=pageColumn.ID
left join UBF_Assemble_Page as page on pageColumn.Page=page.ID
left join UBF_Assemble_Menu as menu on page.URI=menu.URI
left join UBF_Assemble_Menu as pmenu on menu.Parent=pmenu.ID"
            + whereClause;

            var table = ExecuteErpQuery(sql);
            var favorites = GetFavoritedKeys("form");
            var clicks = GetClickStats("form");
            var recents = GetRecentViews("form");
            var notes = GetNotes("form");

            var result = table.Rows.Cast<DataRow>().Select(row =>
            {
                var itemKey = Convert.ToString(GetValue(row, "FormID")) ?? string.Empty;
                ClickInfo click;
                clicks.TryGetValue(itemKey, out click);
                DateTime? viewedAt;
                recents.TryGetValue(itemKey, out viewedAt);
                string note;
                notes.TryGetValue(itemKey, out note);

                return new
                {
                    AssemblyName = Convert.ToString(GetValue(row, "AssemblyName")),
                    ClassName = Convert.ToString(GetValue(row, "ClassName")),
                    FormID = itemKey,
                    Name = Convert.ToString(GetValue(row, "Name")),
                    Url = Convert.ToString(GetValue(row, "Url")),
                    Application = Convert.ToString(GetValue(row, "Application")),
                    MenuName = Convert.ToString(GetValue(row, "MenuName")),
                    ItemType = "form",
                    ItemKey = itemKey,
                    IsFavorite = favorites.Contains(itemKey),
                    Note = note ?? string.Empty,
                    ClickCount = click == null ? 0 : click.ClickCount,
                    LastClickedAt = click == null ? (DateTime?)null : click.LastClickedAt,
                    LastViewedAt = viewedAt,
                    MatchRank = GetMatchRank(safeKeyword,
                        Convert.ToString(GetValue(row, "Name")),
                        Convert.ToString(GetValue(row, "MenuName")),
                        Convert.ToString(GetValue(row, "Url")),
                        Convert.ToString(GetValue(row, "ClassName")),
                        itemKey),
                    MatchLength = GetMatchLength(safeKeyword,
                        Convert.ToString(GetValue(row, "Name")),
                        Convert.ToString(GetValue(row, "MenuName")),
                        Convert.ToString(GetValue(row, "Url")),
                        Convert.ToString(GetValue(row, "ClassName")),
                        itemKey)
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
}
