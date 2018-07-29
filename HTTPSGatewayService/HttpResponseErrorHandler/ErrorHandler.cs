using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Dispatcher;

namespace HttpResponseErrorHandler
{
	public static class ErrorHandler
	{
		public static void Configure(HttpConfiguration config)
		{
			// If a client of your HTTP service send a request to a resource(uri) and no route matched with this uri on server then you can route the request to the above Handle404 method using a custom route.
			// Put this route at the very bottom of route configuration.
			config.Routes.MapHttpRoute(
				name: "Error404",
				routeTemplate: "{*url}",
				defaults: new { controller = "Error", action = "Handle404" }
			);

			config.Services.Replace(typeof(IHttpControllerSelector), new HttpNotFoundAwareDefaultHttpControllerSelector(config));
			config.Services.Replace(typeof(IHttpActionSelector), new HttpNotFoundAwareControllerActionSelector());
		}

		public static void AddServiceFabricHeader(this HttpResponseMessage responseMessage)
		{
			responseMessage.Headers.Add("X-ServiceFabric", "ResourceNotFound");
		}
	}
}
