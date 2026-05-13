using System;
using System.Web.Http;

public class CustomWebGlobal : U9.Subsidiary.Lib.WebGlobal
{
    private static bool _apiConfigured;
    private static readonly object _apiConfigLock = new object();

    static CustomWebGlobal()
    {
        EnsureApiConfigured();
    }

    protected new void Application_Start(object sender, EventArgs e)
    {
        base.Application_Start(sender, e);
        EnsureApiConfigured();
        FileLogger.Info("ClassView application started");
    }

    private static void EnsureApiConfigured()
    {
        if (_apiConfigured) return;

        lock (_apiConfigLock)
        {
            if (_apiConfigured) return;
            var config = GlobalConfiguration.Configuration;
            ConfigureWebApi(config);
            config.EnsureInitialized();
            _apiConfigured = true;
        }
    }

    private static void ConfigureWebApi(HttpConfiguration config)
    {
        config.MapHttpAttributeRoutes();
        config.Filters.Add(new ApiLogFilter());

        config.Routes.MapHttpRoute(
            name: "DefaultApi",
            routeTemplate: "api/{controller}/{action}/{id}",
            defaults: new { id = RouteParameter.Optional }
        );

        config.Formatters.Remove(config.Formatters.XmlFormatter);
        config.Formatters.JsonFormatter.SerializerSettings.ReferenceLoopHandling =
            Newtonsoft.Json.ReferenceLoopHandling.Ignore;
    }
}
