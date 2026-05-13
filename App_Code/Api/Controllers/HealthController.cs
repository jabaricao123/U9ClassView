using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;

public class HealthController : BaseApiController
{
    [HttpGet]
    public HttpResponseMessage Check()
    {
        try
        {
            var erpOk = false;
            try
            {
                erpOk = TestErpConnection(ErpConnectionString);
            }
            catch
            {
                erpOk = false;
            }

            var metaOk = false;
            try
            {
                ExecuteMetaScalar("select 1");
                metaOk = true;
            }
            catch
            {
                metaOk = false;
            }

            return Request.CreateResponse(HttpStatusCode.OK, new
            {
                success = true,
                data = new
                {
                    erp = erpOk,
                    meta = metaOk,
                    serverTime = DateTime.Now
                }
            });
        }
        catch (Exception ex)
        {
            return Request.CreateResponse(HttpStatusCode.OK, new { success = false, message = ex.Message });
        }
    }
}
