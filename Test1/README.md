# Service Fabric Reverse Proxy
A reverse proxy for accessing services in a Service Fabric cluster. The reverse proxy is intended to behave like the [reverse proxy available as a service](https://azure.microsoft.com/en-us/documentation/articles/service-fabric-reverseproxy/) in a hosted Service Fabric cluster. The use is meant  for development environments on [on-premise Service Fabric clusters](https://azure.microsoft.com/en-us/documentation/articles/service-fabric-deploy-anywhere/), e.g., like the sample dev cluster running on Vagrant boxes, see [blog post](http://spoorendonk.dk/?p=36) or [github repo](https://github.com/spoorendonk/service-fabric-box).  

The reverse proxy allows access to Service Fabric services that exposes HTTP endpoints. This provides a single point of entry for all services and a generic way to communicate between services. The proxy may be running on all nodes for high availability. For accessing the services in the cluster from the outside the proxy can be put behind another reverse proxy and load balancer such as [nginx](https://nginx.org/) or [HAProxy](http://www.haproxy.org/).

The `ServicePartitionResolver` resolves the requested service partition closest to the proxy according to the [documentation](https://msdn.microsoft.com/en-us/library/mt124034.aspx). The HTTP listener endpoints for a partition appears to be returned in a round-robin fashion.   

## Usage
The URI format for forwarding request is:
```
http(s)://<Cluster FQDN | internal IP>:Port/<ServiceInstanceName>/<Suffix path>?PartitionKey=<key>&PartitionKind=<partitionkind>&Timeout=<timeout_in_seconds>
```
See the detailed [parameter explanation](https://azure.microsoft.com/en-us/documentation/articles/service-fabric-reverseproxy/#uri-format-for-addressing-services-via-the-reverse-proxy) 

The `<Cluster FQDN | internal IP>` is the cluster address, the  `<ServiceInstanceName>` is the  fully-qualified deployed service instance name without the `fabric:/` scheme, `<Suffix path>` is the URL path, `<key>`  is the partition key, `<paritionkind>` is either `Int64Range` or `Named`, and `<timeout>` is the proxy timeout in seconds. Note that the `<ServiceInstanceName>` is case sensitive.

## Special handling for port-sharing services
This section addresses the problem described [here](https://azure.microsoft.com/en-us/documentation/articles/service-fabric-reverseproxy/#special-handling-for-port-sharing-services).

In short the reverse proxy attempts to re-resolve a service address and retry requests if they fail. However, if services are moved they can become unreachable but the web server may still be available and the request would result in a 404 (not found). As a result, an HTTP 404 has two distinct meanings:

1. The service address is correct, but the resource requested by the user does not exist.
2. The service address is incorrect, and the resource requested by the user may actually exist on a different node.

The first case is a considered a user error, but the second is because the service was moved and should result in a re-resolve and a retry. To distinguish, the proxy assumes case 2, but to indicate case 1 the services responding with a 404 (not found) should include the following HTTP response header:

```
X-ServiceFabric : ResourceNotFound
```

## App root entry point
When accessing an HTTP endpoint behind a reverse proxy, relative calls to the service may also go through the proxy. To accommodate this situation it is possible to point the root of the service to be
```
<ServiceInstanceName>/
```  
Now, it is transparent if it is called directly or through the reverse proxy. 

## Example
Let's deploy the reverse proxy on a local cluster and use it to access the sample [*WordCount*](https://github.com/Azure-Samples/service-fabric-dotnet-getting-started/tree/master/Services/WordCount) app. If you deploy somewhere else modify the `localhost` entry with the correct cluster address in the following.

1. Modify the *WordCount* app to a different startup root. Modify the `CreateServiceInstanceListeners()` method in `WordCountWebService.cs` to
```csharp
protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
{
    return new[]
    {
        new ServiceInstanceListener(initParams => new OwinCommunicationListener("WordCount/WordCountWebService", new Startup(), initParams))            
    };
}
```

2. Deploy the *WordCount* and the *ReverseProxy* applications to the Service Fabric.

3. Verify that the *WordCount* app is running at by accessing it directly
```
http://localhost:8081/WordCount/WordCountWebService/
```

4. Access the *WordCount* app through the reverse proxy on the address
```
http://localhost:8080/WordCount/WordCountWebService/
```
The slash in the end is important for proxy to forward the request correctly.

One can verify that the reverse proxy can still access the `WordCount` app when the port number is unspecified and more instances of the web service is started. In the former case modifying the `EndPoint` element in the `WordCountWebService\PackageRoot\ServiceManifest.xml` file, and in the latter case increase the value of the `WordCountWebService_InstanceCount` in the `WordCount\ApplicationParameters\Local.xml` file.

In this case the Service Fabric automatically set the port numbers for the instances and the reverse proxy resolves the addresses.

The idea is the same for deploying on an on-premise cluster. If there are several instances of the `WordCountWebService` running the reverse proxy picks one of them when forwarding the request. As mentioned before the `ReverseProxy` app may be running on all nodes and can be load balanced by another reverse proxy. Alternatively, only one instance of `ReverseProxy` is deployed to the Service Fabric cluster and that cn act as the single point of entry into the cluster.

## Implementation
In this section we take dive into the code.

### Proxying
The proxying part follows the implementation from this [blog post](http://kasperholdum.dk/2016/03/reverse-proxy-in-asp-net-web-api/) by Kasper Holdum.

The idea is to add a message handler to the `MessageHandlers` property of the `HttpConfiguration` class that takes care of redirecting messages. This is the `ProxyHandler` that inherits from `DelegatingHandler`
```csharp
public static class Startup
{
    public static void ConfigureApp(IAppBuilder appBuilder)
    {
        HttpConfiguration config = new HttpConfiguration();
        config.MessageHandlers.Add(new ProxyHandler());
        config.Routes.MapHttpRoute("ReverseProxy", "{*path}");
        appBuilder.UseWebApi(config);
    }
}
```
By overriding the `SendAsync` method in the `ProxyHandler` class it is possible to redirect requests.  

### Resolving Service Fabric Endpoints
The URL is parsed to identify the correct service name, suffix path and partition infomation. The following code snippet initilizes the cancellation tokens with a timeout, resolves the service partition, and initilizes the build-in retry loop for communicating with the service partition:
```csharp
var cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromSeconds(timeout));

var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts.Token);

try
{
    var partitionClient = new ServicePartitionClient<HttpCommunicationClient>(communicationFactory, new Uri(servicePath), servicePartitionKey);

    return await partitionClient.InvokeWithRetryAsync(async (client) =>
    {
        var clonedRequest = await HttpRequestMessageExtensions.CloneHttpRequestMessageAsync(request);
        clonedRequest.RequestUri = new Uri(client.Url, suffixPath + "?" + query);

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
```  

Inside the loop, the HTTP request is cloned with the extension method `HttpRequestMessageExtensions.CloneHttpRequestMessageAsync` steeming from [this answer on stack overflow](http://stackoverflow.com/a/34049029/71515).

If a 404 (not found) response is received by the proxy it checks for a `X-ServiceFabric` header with value `ResourceNotFound` otherwise it throws a `FabricServiceNotFoundException` exception. That is, if the upstream correctly includes the `X-ServiceFabric` header in it's 404 responses than these are passed unchanged back to the client otherwise the proxy specifies that the service is not found because it may have been moved. 

### Error Handling of 404
The [`HttpResponseErrorHandler`](https://github.com/spoorendonk/service-fabric-reverse-proxy/tree/master/HttpResponseErrorHandler) follows the [blog post](http://weblogs.asp.net/imranbaloch/handling-http-404-error-in-asp-net-web-api) by Imran Baloch for handling 404 responses from a web server.

Basically an `ErrorController` is registered to capture all 404 response and allow for customizing the response. In this case adding the `X-ServiceFabric` header.
The static `Configure` method in the `ErrorHandler` class adds a route to the `ErrorController` and replaces the HTTP controller and action selectors.

The `WebApp` is configured by modifying the `HttpConfiguration` in the `Action<IAppBuilder>` method injected into the `WebApp.Start` method. An example configure method could be:
```csharp
public static class Startup
{
    public static void ConfigureApp(IAppBuilder appBuilder)
    {
        HttpConfiguration config = new HttpConfiguration();
        ErrorHandler.Configure(config);
        appBuilder.UseWebApi(config);
    }
}
```




 