using System.Net;
using System.Web.Http;
using System.Web.Http.Controllers;

namespace HttpResponseErrorHandler
{
	/// <summary>
	/// From http://weblogs.asp.net/imranbaloch/handling-http-404-error-in-asp-net-web-api
	/// 
	/// It is also required to pass the request to the above Handle404 method if no matching action method found in the selected controller due to the reason discussed above. 
	/// This situation can also be easily handled through a custom IHttpActionSelector
	/// </summary>
	internal class HttpNotFoundAwareControllerActionSelector : ApiControllerActionSelector
	{
		public override HttpActionDescriptor SelectAction(HttpControllerContext controllerContext)
		{
			HttpActionDescriptor decriptor = null;
			try
			{
				decriptor = base.SelectAction(controllerContext);
			}
			catch (HttpResponseException ex)
			{
				var code = ex.Response.StatusCode;
				if (code != HttpStatusCode.NotFound && code != HttpStatusCode.MethodNotAllowed)
					throw;

				var routeData = controllerContext.RouteData;
				routeData.Values["action"] = "Handle404";
				IHttpController httpController = new ErrorController();
				controllerContext.Controller = httpController;
				controllerContext.ControllerDescriptor = new HttpControllerDescriptor(controllerContext.Configuration, "Error", httpController.GetType());
				decriptor = base.SelectAction(controllerContext);

			}
			return decriptor;

		}

	}
}