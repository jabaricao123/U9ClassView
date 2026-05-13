using System;
using System.Diagnostics;
using System.Net;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

public class ApiLogFilter : ActionFilterAttribute
{
    private const string StopwatchKey = "ClassView.ApiStopwatch";

    public override void OnActionExecuting(HttpActionContext actionContext)
    {
        actionContext.Request.Properties[StopwatchKey] = Stopwatch.StartNew();
        FileLogger.Info("API start " + GetActionName(actionContext));
        base.OnActionExecuting(actionContext);
    }

    public override void OnActionExecuted(HttpActionExecutedContext actionExecutedContext)
    {
        var elapsed = GetElapsedMilliseconds(actionExecutedContext);
        var statusCode = actionExecutedContext.Response == null
            ? HttpStatusCode.InternalServerError
            : actionExecutedContext.Response.StatusCode;

        if (actionExecutedContext.Exception != null)
        {
            FileLogger.Error(
                "API error status=" + (int)statusCode + " elapsedMs=" + elapsed,
                actionExecutedContext.Exception);
        }
        else
        {
            FileLogger.Info("API end status=" + (int)statusCode + " elapsedMs=" + elapsed);
        }

        base.OnActionExecuted(actionExecutedContext);
    }

    private static string GetActionName(HttpActionContext actionContext)
    {
        if (actionContext == null || actionContext.ActionDescriptor == null)
        {
            return string.Empty;
        }

        return actionContext.ActionDescriptor.ControllerDescriptor.ControllerName
            + "."
            + actionContext.ActionDescriptor.ActionName;
    }

    private static long GetElapsedMilliseconds(HttpActionExecutedContext actionExecutedContext)
    {
        object value;
        if (actionExecutedContext != null
            && actionExecutedContext.Request != null
            && actionExecutedContext.Request.Properties.TryGetValue(StopwatchKey, out value))
        {
            var stopwatch = value as Stopwatch;
            if (stopwatch != null)
            {
                stopwatch.Stop();
                return stopwatch.ElapsedMilliseconds;
            }
        }

        return 0;
    }
}
