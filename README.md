# Gateway Service using Reverse Proxy with Service Fabric - http/https
Gateway Service with Reverse Proxy using Service Fabric - Samples 

Reverse proxy works like a Gateway Service in Service Fabric as per the documentation below
https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-reverseproxy

Pre-Requisites:
- https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-get-started
- Install .net Core 2.1 or Later
- Run Visual Studio in Local Administrator Mode for Servie Fabric Deployment

Note: 
 - If you are using windows 10 or above, please perform latest windows updates or latest version of windows. Ensure Service Fabric SDK is latest installed.
 - Ensure Service Fabric Cluster is Running in local - http://localhost:19080/Explorer/index.html


I have added two folders
1. Service Fabric with one sample Web1 Service. you can test the service as explained below Test1
2. Service Fabric App Layer accessed from the above deployed Reverse Proxy Gateway. With .Net Core API
Service Fabric Reverse Proxy - Gateway

Usage:
	http(s)://<Cluster FQDN | internal IP>:Port/<ServiceInstanceName>/<Suffix path>?PartitionKey=<key>&PartitionKind=<partitionkind>&Timeout=<timeout_in_seconds>

Test1: 
  http://localhost:8080/ReverseProxy/ReverseProxyService/ReverseProxy/Web1/api/values
  
Test2:
  http://localhost:8080/ReverseProxy/ReverseProxyService/Application4/Web2/api/values

Gateway Service URL : http://localhost:8080/ReverseProxy/ReverseProxyService/

Note: <ServiceInstanceName> and <Suffix path> are case sensitive

Services responding with a 404 (not found) should include the following HTTP response header:
	X-ServiceFabric : ResourceNotFound

Deployment of Service Using Powershell to Service Fabric cluster:
https://docs.microsoft.com/en-us/azure/service-fabric/scripts/service-fabric-powershell-deploy-application
