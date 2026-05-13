using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

public class NoteController : BaseApiController
{
    [HttpGet]
    public HttpResponseMessage List(string itemType = null)
    {
        try
        {
            var sql = @"
select
    item_type as ItemType,
    item_key as ItemKey,
    note as Note,
    updated_at as UpdatedAt
from item_note";

            Dictionary<string, object> parameters = null;
            if (!string.IsNullOrWhiteSpace(itemType))
            {
                sql += " where item_type=@item_type ";
                parameters = new Dictionary<string, object> { { "@item_type", itemType } };
            }

            sql += " order by updated_at desc ";
            var table = ExecuteMetaQuery(sql, parameters);
            var result = table.Rows.Cast<DataRow>().Select(row => new
            {
                ItemType = Convert.ToString(GetValue(row, "ItemType")),
                ItemKey = Convert.ToString(GetValue(row, "ItemKey")),
                Note = Convert.ToString(GetValue(row, "Note")),
                UpdatedAt = ConvertToDateTime(GetValue(row, "UpdatedAt"))
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
            FileLogger.Warn("Meta unavailable, note list returns empty: " + ex.Message);
            return Request.CreateResponse(HttpStatusCode.OK, new { success = true, data = new object[0], total = 0, metaAvailable = false });
        }
    }

    [HttpPost]
    public HttpResponseMessage Save([FromBody] NoteSaveRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ItemType) || string.IsNullOrWhiteSpace(request.ItemKey))
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { success = false, message = "参数不能为空" });
            }

            ExecuteMetaNonQuery(
                @"insert into item_note(item_type, item_key, note, created_at, updated_at)
                  values(@item_type, @item_key, @note, current_timestamp, current_timestamp)
                  on conflict(item_type, item_key) do update set
                    note=excluded.note,
                    updated_at=current_timestamp",
                new Dictionary<string, object>
                {
                    { "@item_type", request.ItemType },
                    { "@item_key", request.ItemKey },
                    { "@note", request.Note ?? string.Empty }
                });

            return Request.CreateResponse(HttpStatusCode.OK, new { success = true });
        }
        catch (Exception ex)
        {
            return Request.CreateResponse(HttpStatusCode.OK, new { success = false, message = ex.Message });
        }
    }
}

public class NoteSaveRequest
{
    public string ItemType { get; set; }
    public string ItemKey { get; set; }
    public string Note { get; set; }
}
