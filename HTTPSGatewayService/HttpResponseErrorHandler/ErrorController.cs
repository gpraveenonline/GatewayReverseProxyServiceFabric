using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace HttpResponseErrorHandler
{
	/// <summary>
	/// From http://weblogs.asp.net/imranbaloch/handling-http-404-error-in-asp-net-web-api
	/// 
	/// Handle HTTP 404 errors in a centralized location. From ASP.NET Web API point of you, you need to handle these situations,
	/// 
	/// <list type="bullet">
	/// <item>No route matched.</item>
	/// <item>Route is matched but no {controller} has been found on route.</item>
	/// <item>No type with {controller} name has been found.</item>
	/// <item></item>No matching action method found in the selected controller due to no action method start with the request HTTP method verb or no action method with IActionHttpMethodProviderRoute implemented attribute found or no method with {action} name found or no method with the matching {action} name found.</item>
	/// </list>
	/// 
	/// This action method will be used in all of the above cases for sending HTTP 404 response message to the client.
	/// </summary>
	internal class ErrorController : ApiController
	{
		[HttpGet, HttpPost, HttpPut, HttpDelete, HttpHead, HttpOptions, AcceptVerbs("PATCH")]
		public HttpResponseMessage Handle404()
		{
			var responseMessage = new HttpResponseMessage(HttpStatusCode.NotFound);

			// Add Service Fabric header
			responseMessage.AddServiceFabricHeader();

			return responseMessage;
		}
	}
}