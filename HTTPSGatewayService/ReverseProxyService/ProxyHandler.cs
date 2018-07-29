using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HttpResponseErrorHandler;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Client;

namespace ReverseProxyService
{
	/// <summary>
	/// Inspired by http://kasperholdum.dk/2016/03/reverse-proxy-in-asp-net-web-api/
	/// </summary>
	internal class ProxyHandler : DelegatingHandler
	{
		private readonly FabricClient fabricClient;
		private readonly HttpCommunicationClientFactory communicationFactory;

		public ProxyHandler() : base()
		{
			fabricClient = new FabricClient();

			communicationFactory = new HttpCommunicationClientFactory(new ServicePartitionResolver(() => fabricClient));
		}

		private async Task<HttpResponseMessage> RedirectRequest(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			if (request.RequestUri.Segments.Length < 3)
			{
				return new HttpResponseMessage(HttpStatusCode.BadRequest);
			}

			// resolve http(s)://<Cluster FQDN | internal IP>:Port/<ServiceInstanceName>/<Suffix path>?PartitionKey=<key>&PartitionKind=<partitionkind>&Timeout=<timeout_in_seconds>
			string servicePath;
			string suffixPath;

			ProxyHandlerHelper.BuildFabricPath(request.RequestUri, out servicePath, out suffixPath);

			// parse query string
			var queryCollection = request.RequestUri.ParseQueryString();

			string query = ProxyHandlerHelper.BuildQuery(queryCollection);

			string partitionKind;
			if (!ProxyHandlerHelper.TryGetQueryValue(queryCollection, ProxyHandlerHelper.QueryKey.PartitionKind, out partitionKind))
			{
				partitionKind = null;
			}

			string partitionKey;
			if (!ProxyHandlerHelper.TryGetQueryValue(queryCollection, ProxyHandlerHelper.QueryKey.PartitionKey, out partitionKey))
			{
				partitionKey = null;
			}

			int timeout;
			if (!ProxyHandlerHelper.TryGetQueryValue(queryCollection, ProxyHandlerHelper.QueryKey.Timeout, out timeout))
			{
				timeout = 60;
			}

			// Get partition key
			ServicePartitionKey servicePartitionKey = null;

			if (partitionKind != null && partitionKey != null)
			{
				servicePartitionKey = ProxyHandlerHelper.GetServicePartitionKey(partitionKind, partitionKey);
			}

			// Setup cancellation tokens
			var cts = new CancellationTokenSource();
			cts.CancelAfter(TimeSpan.FromSeconds(timeout));

			var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

			try
			{
				var partitionClient = new ServicePartitionClient<HttpCommunicationClient>(communicationFactory, new Uri(servicePath), servicePartitionKey);

				return await partitionClient.InvokeWithRetryAsync(async (client) =>
					{
						var clonedRequest = await HttpRequestMessageExtensions.CloneHttpRequestMessageAsync(request);
						clonedRequest.RequestUri = new Uri(client.Url, suffixPath + query);

						var upstreamResponse = await client.HttpClient.SendAsync(clonedRequest, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);

						IEnumerable<string> serviceFabricHeader;
						if (upstreamResponse.StatusCode == HttpStatusCode.NotFound
							&& (!upstreamResponse.Headers.TryGetValues("X-ServiceFabric", out serviceFabricHeader) || !serviceFabricHeader.Contains("ResourceNotFound")))
						{
							throw new FabricServiceNotFoundException();
						}

						return upstreamResponse;
					}, linkedCts.Token);

			}
			catch (FabricServiceNotFoundException)
			{
				var response = new HttpResponseMessage(HttpStatusCode.NotFound);
				response.AddServiceFabricHeader();

				return response;
			}
			catch (TaskCanceledException)
			{
				return new HttpResponseMessage(HttpStatusCode.GatewayTimeout);
			}
			catch (Exception)
			{
				return new HttpResponseMessage(HttpStatusCode.InternalServerError);
			}
		}


		private async Task<HttpResponseMessage> GetRoot()
		{
			var message = new HttpResponseMessage(HttpStatusCode.OK);
			message.Content = new StringContent(
@"A Service Fabric Reverse Proxy

Should behave like https://azure.microsoft.com/en-us/documentation/articles/service-fabric-reverseproxy/

Usage:
	http(s)://<Cluster FQDN | internal IP>:Port/<ServiceInstanceName>/<Suffix path>?PartitionKey=<key>&PartitionKind=<partitionkind>&Timeout=<timeout_in_seconds>

Note: <ServiceInstanceName> and <Suffix path> are case sensitive

Services responding with a 404 (not found) should include the following HTTP response header:
	X-ServiceFabric : ResourceNotFound"
);

			return await Task.FromResult(message);
		}

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			// root
			if (request.RequestUri.PathAndQuery == "/")
			{
				return await GetRoot();
			}

			return await RedirectRequest(request, cancellationToken);
		}
	}
}