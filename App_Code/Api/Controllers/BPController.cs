using System;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

public class BPController : BaseApiController
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
                    whereClause = " and b.DisplayName like '%" + safeKeyword + "%' ";
                }
                else
                {
                    whereClause = " and b.DisplayName = '" + safeKeyword + "' ";
                }
            }

            var sql = @"
select
    b.DisplayName as DisplayName,
    a.FullName as FullName,
    c.AssemblyName as AssemblyName,
    c.Kind as Kind,
    d.DisplayName as ComponentDisplayName
from UBF_MD_Class as a
left join UBF_MD_Class_Trl as b on a.Local_ID=b.Local_ID
left join UBF_MD_Component as c on a.MD_Component_ID=c.ID
left join UBF_MD_Component_Trl as d on c.Local_ID=d.Local_ID
where c.Kind='BP'"
            + whereClause;

            var table = ExecuteErpQuery(sql);
            var favorites = GetFavoritedKeys("bp");
            var clicks = GetClickStats("bp");
            var recents = GetRecentViews("bp");
            var notes = GetNotes("bp");

            var result = table.Rows.Cast<DataRow>().Select(row =>
            {
                var itemKey = Convert.ToString(GetValue(row, "FullName")) ?? string.Empty;
                ClickInfo click;
                clicks.TryGetValue(itemKey, out click);
                DateTime? viewedAt;
                recents.TryGetValue(itemKey, out viewedAt);
                string note;
                notes.TryGetValue(itemKey, out note);

                return new
                {
                    DisplayName = Convert.ToString(GetValue(row, "DisplayName")),
                    FullName = itemKey,
                    AssemblyName = Convert.ToString(GetValue(row, "AssemblyName")),
                    Kind = Convert.ToString(GetValue(row, "Kind")),
                    ComponentDisplayName = Convert.ToString(GetValue(row, "ComponentDisplayName")),
                    ItemType = "bp",
                    ItemKey = itemKey,
                    IsFavorite = favorites.Contains(itemKey),
                    Note = note ?? string.Empty,
                    ClickCount = click == null ? 0 : click.ClickCount,
                    LastClickedAt = click == null ? (DateTime?)null : click.LastClickedAt,
                    LastViewedAt = viewedAt,
                    MatchRank = GetMatchRank(safeKeyword,
                        Convert.ToString(GetValue(row, "DisplayName")),
                        itemKey,
                        Convert.ToString(GetValue(row, "ComponentDisplayName"))),
                    MatchLength = GetMatchLength(safeKeyword,
                        Convert.ToString(GetValue(row, "DisplayName")),
                        itemKey,
                        Convert.ToString(GetValue(row, "ComponentDisplayName")))
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
