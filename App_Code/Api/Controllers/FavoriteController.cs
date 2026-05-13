using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

public class FavoriteController : BaseApiController
{
    [HttpGet]
    public HttpResponseMessage List(string itemType = null)
    {
        try
        {
            var sql = @"
select
    id as Id,
    item_type as ItemType,
    item_key as ItemKey,
    title as Title,
    subtitle as Subtitle,
    extra_json as ExtraJson,
    created_at as CreatedAt
from favorite_item";

            Dictionary<string, object> parameters = null;
            if (!string.IsNullOrWhiteSpace(itemType))
            {
                sql += " where item_type=@item_type ";
                parameters = new Dictionary<string, object> { { "@item_type", itemType } };
            }

            sql += " order by created_at desc ";

            var table = ExecuteMetaQuery(sql, parameters);
            var result = table.Rows.Cast<DataRow>().Select(row => new
            {
                Id = ConvertToInt(GetValue(row, "Id")),
                ItemType = Convert.ToString(GetValue(row, "ItemType")),
                ItemKey = Convert.ToString(GetValue(row, "ItemKey")),
                Title = Convert.ToString(GetValue(row, "Title")),
                Subtitle = Convert.ToString(GetValue(row, "Subtitle")),
                ExtraJson = Convert.ToString(GetValue(row, "ExtraJson")),
                CreatedAt = Convert.ToDateTime(GetValue(row, "CreatedAt"))
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
            FileLogger.Warn("Meta unavailable, favorite list returns empty: " + ex.Message);
            return Request.CreateResponse(HttpStatusCode.OK, new { success = true, data = new object[0], total = 0, metaAvailable = false });
        }
    }

    [HttpPost]
    public HttpResponseMessage Toggle([FromBody] FavoriteToggleRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ItemType) || string.IsNullOrWhiteSpace(request.ItemKey))
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { success = false, message = "参数不能为空" });
            }

            var exists = ExecuteMetaScalar(
                "select id from favorite_item where item_type=@item_type and item_key=@item_key limit 1",
                new Dictionary<string, object>
                {
                    { "@item_type", request.ItemType },
                    { "@item_key", request.ItemKey }
                });

            var isFavorited = false;
            if (exists == null || exists == DBNull.Value)
            {
                ExecuteMetaNonQuery(
                    @"insert into favorite_item(item_type, item_key, title, subtitle, extra_json, created_at)
                      values(@item_type, @item_key, @title, @subtitle, @extra_json, current_timestamp)",
                    new Dictionary<string, object>
                    {
                        { "@item_type", request.ItemType },
                        { "@item_key", request.ItemKey },
                        { "@title", request.Title ?? string.Empty },
                        { "@subtitle", request.Subtitle ?? string.Empty },
                        { "@extra_json", (object)request.ExtraJson ?? DBNull.Value }
                    });
                isFavorited = true;
            }
            else
            {
                ExecuteMetaNonQuery(
                    "delete from favorite_item where item_type=@item_type and item_key=@item_key",
                    new Dictionary<string, object>
                    {
                        { "@item_type", request.ItemType },
                        { "@item_key", request.ItemKey }
                    });
            }

            return Request.CreateResponse(HttpStatusCode.OK, new
            {
                success = true,
                data = new { isFavorited = isFavorited }
            });
        }
        catch (Exception ex)
        {
            return Request.CreateResponse(HttpStatusCode.OK, new { success = false, message = ex.Message });
        }
    }
}

public class FavoriteToggleRequest
{
    public string ItemType { get; set; }
    public string ItemKey { get; set; }
    public string Title { get; set; }
    public string Subtitle { get; set; }
    public string ExtraJson { get; set; }
}
