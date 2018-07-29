using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Dispatcher;

namespace HttpResponseErrorHandler
{
	/// <summary>
	/// From http://weblogs.asp.net/imranbaloch/handling-http-404-error-in-asp-net-web-api
	/// 
	/// Need to handle the case when there is no {controller} in the matching route or when there is no type with {controller} name found.
	/// Route the request to the Handle404 method using a custom IHttpControllerSelector.
	/// </summary>
	internal class HttpNotFoundAwareDefaultHttpControllerSelector : DefaultHttpControllerSelector
	{
		public HttpNotFoundAwareDefaultHttpControllerSelector(HttpConfiguration configuration)
			: base(configuration)
		{
		}

		public override HttpControllerDescriptor SelectController(HttpRequestMessage request)
		{
			HttpControllerDescriptor decriptor = null;
			try
			{
				decriptor = base.SelectController(request);
			}
			catch (HttpResponseException ex)
			{
				var code = ex.Response.StatusCode;
				if (code != HttpStatusCode.NotFound)
					throw;

				var routeValues = request.GetRouteData().Values;
				routeValues["controller"] = "Error";
				routeValues["action"] = "Handle404";
				decriptor = base.SelectController(request);
			}

			return decriptor;
		}
	}
}