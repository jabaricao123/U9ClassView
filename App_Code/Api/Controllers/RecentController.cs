using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

public class RecentController : BaseApiController
{
    [HttpGet]
    public HttpResponseMessage List(string itemType = null, int top = 100)
    {
        try
        {
            if (top <= 0)
            {
                top = 100;
            }

            var sql = @"
select
    r.item_type as ItemType,
    r.item_key as ItemKey,
    r.title as Title,
    r.subtitle as Subtitle,
    r.extra_json as ExtraJson,
    r.viewed_at as ViewedAt,
    coalesce(c.click_count, 0) as ClickCount,
    c.last_clicked_at as LastClickedAt
from recent_view r
left join click_stat c on r.item_type=c.item_type and r.item_key=c.item_key";

            Dictionary<string, object> parameters = new Dictionary<string, object>();
            if (!string.IsNullOrWhiteSpace(itemType))
            {
                sql += " where r.item_type=@item_type ";
                parameters.Add("@item_type", itemType);
            }

            sql += " order by r.viewed_at desc limit @top ";
            parameters.Add("@top", top);

            var table = ExecuteMetaQuery(sql, parameters);
            var result = table.Rows.Cast<DataRow>().Select(row => new
            {
                ItemType = Convert.ToString(GetValue(row, "ItemType")),
                ItemKey = Convert.ToString(GetValue(row, "ItemKey")),
                Title = Convert.ToString(GetValue(row, "Title")),
                Subtitle = Convert.ToString(GetValue(row, "Subtitle")),
                ExtraJson = Convert.ToString(GetValue(row, "ExtraJson")),
                ViewedAt = ConvertToDateTime(GetValue(row, "ViewedAt")),
                ClickCount = ConvertToInt(GetValue(row, "ClickCount")),
                LastClickedAt = ConvertToDateTime(GetValue(row, "LastClickedAt"))
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
            FileLogger.Warn("Meta unavailable, recent list returns empty: " + ex.Message);
            return Request.CreateResponse(HttpStatusCode.OK, new { success = true, data = new object[0], total = 0, metaAvailable = false });
        }
    }

    [HttpPost]
    public HttpResponseMessage Record([FromBody] RecentRecordRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ItemType) || string.IsNullOrWhiteSpace(request.ItemKey))
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { success = false, message = "参数不能为空" });
            }

            RecordRecentView(request.ItemType, request.ItemKey, request.Title, request.Subtitle, request.ExtraJson);
            return Request.CreateResponse(HttpStatusCode.OK, new { success = true });
        }
        catch (Exception ex)
        {
            return Request.CreateResponse(HttpStatusCode.OK, new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public HttpResponseMessage Click([FromBody] RecentClickRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ItemType) || string.IsNullOrWhiteSpace(request.ItemKey))
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { success = false, message = "参数不能为空" });
            }

            IncrementClick(request.ItemType, request.ItemKey);
            return Request.CreateResponse(HttpStatusCode.OK, new { success = true });
        }
        catch (Exception ex)
        {
            return Request.CreateResponse(HttpStatusCode.OK, new { success = false, message = ex.Message });
        }
    }
}

public class RecentRecordRequest
{
    public string ItemType { get; set; }
    public string ItemKey { get; set; }
    public string Title { get; set; }
    public string Subtitle { get; set; }
    public string ExtraJson { get; set; }
}

public class RecentClickRequest
{
    public string ItemType { get; set; }
    public string ItemKey { get; set; }
}
