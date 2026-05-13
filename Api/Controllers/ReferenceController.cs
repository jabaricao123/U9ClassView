using System;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

public class ReferenceController : BaseApiController
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
                        " where c.RefEntityName like '%{0}%' OR d.DisplayName like '%{0}%' OR a.FormId like '%{0}%' ",
                        safeKeyword);
                }
                else
                {
                    whereClause = string.Format(
                        " where c.RefEntityName = '{0}' OR d.DisplayName='{0}' OR a.FormId ='{0}' ",
                        safeKeyword);
                }
            }

            var sql = @"
select
    a.[Assembly] as Assembly,
    a.FormId as FormId,
    b.Name as FormName,
    c.Filter as Filter,
    a.ClassName as ClassName,
    a.URI as Url,
    d.DisplayName as DisplayName,
    c.RefEntityName as RefEntityName
from aspnet_Parts a
left join UBF_MD_UIRComponent b on a.URI=b.URI
left join UBF_MD_UIReference c on b.ID=c.UIReferenceComponent
left join UBF_MD_UIRComponent_Trl d on b.ID=d.ID"
            + whereClause;

            var table = ExecuteErpQuery(sql);
            var favorites = GetFavoritedKeys("reference");
            var clicks = GetClickStats("reference");
            var recents = GetRecentViews("reference");
            var notes = GetNotes("reference");

            var result = table.Rows.Cast<DataRow>().Select(row =>
            {
                var itemKey = Convert.ToString(GetValue(row, "FormId")) ?? string.Empty;
                ClickInfo click;
                clicks.TryGetValue(itemKey, out click);
                DateTime? viewedAt;
                recents.TryGetValue(itemKey, out viewedAt);
                string note;
                notes.TryGetValue(itemKey, out note);

                return new
                {
                    Assembly = Convert.ToString(GetValue(row, "Assembly")),
                    FormId = itemKey,
                    FormName = Convert.ToString(GetValue(row, "FormName")),
                    Filter = Convert.ToString(GetValue(row, "Filter")),
                    ClassName = Convert.ToString(GetValue(row, "ClassName")),
                    Url = Convert.ToString(GetValue(row, "Url")),
                    DisplayName = Convert.ToString(GetValue(row, "DisplayName")),
                    RefEntityName = Convert.ToString(GetValue(row, "RefEntityName")),
                    ItemType = "reference",
                    ItemKey = itemKey,
                    IsFavorite = favorites.Contains(itemKey),
                    Note = note ?? string.Empty,
                    ClickCount = click == null ? 0 : click.ClickCount,
                    LastClickedAt = click == null ? (DateTime?)null : click.LastClickedAt,
                    LastViewedAt = viewedAt,
                    MatchRank = GetMatchRank(safeKeyword,
                        Convert.ToString(GetValue(row, "DisplayName")),
                        Convert.ToString(GetValue(row, "RefEntityName")),
                        Convert.ToString(GetValue(row, "FormName")),
                        Convert.ToString(GetValue(row, "ClassName")),
                        itemKey),
                    MatchLength = GetMatchLength(safeKeyword,
                        Convert.ToString(GetValue(row, "DisplayName")),
                        Convert.ToString(GetValue(row, "RefEntityName")),
                        Convert.ToString(GetValue(row, "FormName")),
                        Convert.ToString(GetValue(row, "ClassName")),
                        itemKey)
                };
            })
            .OrderBy(x => x.MatchRank)
            .ThenBy(x => x.MatchLength)
            .ThenByDescending(x => x.IsFavorite)
            .ThenByDescending(x => x.ClickCount)
            .ThenByDescending(x => x.LastViewedAt ?? DateTime.MinValue)
            .ThenBy(x => x.DisplayName)
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
