using System.Web.Http;
using HttpResponseErrorHandler;
using Owin;

namespace ReverseProxyService
{
	public static class Startup
	{
		// This code configures Web API. The Startup class is specified as a type
		// parameter in the WebApp.Start method.
		public static void ConfigureApp(IAppBuilder appBuilder)
		{
			// Configure Web API for self-host. 
			HttpConfiguration config = new HttpConfiguration();

			config.MessageHandlers.Add(new ProxyHandler());
			config.Routes.MapHttpRoute("ReverseProxy", "{*path}");

			// For sending X-ServiceFabric:ResourceNotFound header on a 404
			ErrorHandler.Configure(config);

			appBuilder.UseWebApi(config);
		}
	}
}
