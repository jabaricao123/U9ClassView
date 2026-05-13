using System;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using UFSoft.UBF.Business;
using UFSoft.UBF.View.Query;

public class OqlController : BaseApiController
{
    [HttpPost]
    public HttpResponseMessage Parse([FromBody] OqlParseRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Oql))
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { success = false, message = "OQL 不能为空" });
            }

            var viewQuery = new EntityViewQuery();
            var query = viewQuery.CreateQuery(request.Oql.Trim());
            var compiled = query.CompiledQuery;

            return Request.CreateResponse(HttpStatusCode.OK, new
            {
                success = true,
                data = compiled.SelectStatement
            });
        }
        catch (Exception ex)
        {
            return Request.CreateResponse(HttpStatusCode.OK, new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public HttpResponseMessage Execute([FromBody] OqlExecuteRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Sql))
            {
                return Request.CreateResponse(HttpStatusCode.OK, new { success = false, message = "SQL 不能为空" });
            }

            var table = ExecuteErpQuery(request.Sql);
            var result = table.Rows.Cast<DataRow>().Select(row =>
            {
                var dict = new System.Collections.Generic.Dictionary<string, object>();
                foreach (DataColumn column in table.Columns)
                {
                    dict[column.ColumnName] = row[column];
                }
                return dict;
            }).ToList();

            return Request.CreateResponse(HttpStatusCode.OK, new
            {
                success = true,
                data = result,
                total = result.Count,
                columns = table.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList()
            });
        }
        catch (Exception ex)
        {
            return Request.CreateResponse(HttpStatusCode.OK, new { success = false, message = ex.Message });
        }
    }
}

public class OqlParseRequest
{
    public string Oql { get; set; }
}

public class OqlExecuteRequest
{
    public string Sql { get; set; }
}
