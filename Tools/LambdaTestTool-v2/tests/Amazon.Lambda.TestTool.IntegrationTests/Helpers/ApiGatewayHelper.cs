// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.APIGateway;
using Amazon.ApiGatewayV2;
using Amazon.APIGateway.Model;
using Amazon.ApiGatewayV2.Model;
using System.Net;
using Amazon.Runtime.Internal.Endpoints.StandardLibrary;

namespace Amazon.Lambda.TestTool.IntegrationTests.Helpers
{
    public class ApiGatewayHelper
    {
        private readonly IAmazonAPIGateway _apiGatewayV1Client;
        private readonly IAmazonApiGatewayV2 _apiGatewayV2Client;
        private readonly HttpClient _httpClient;

        public ApiGatewayHelper(IAmazonAPIGateway apiGatewayV1Client, IAmazonApiGatewayV2 apiGatewayV2Client)
        {
            _apiGatewayV1Client = apiGatewayV1Client;
            _apiGatewayV2Client = apiGatewayV2Client;
            _httpClient = new HttpClient();
        }

        public async Task WaitForApiAvailability(string apiId, string apiUrl, bool isHttpApi, int maxWaitTimeSeconds = 30)
        {
            var startTime = DateTime.UtcNow;
            while ((DateTime.UtcNow - startTime).TotalSeconds < maxWaitTimeSeconds)
            {
                try
                {
                    // Check if the API exists
                    if (isHttpApi)
                    {
                        var response = await _apiGatewayV2Client.GetApiAsync(new GetApiRequest { ApiId = apiId });
                        if (response.ApiEndpoint == null) continue;
                    }
                    else
                    {
                        var response = await _apiGatewayV1Client.GetRestApiAsync(new GetRestApiRequest { RestApiId = apiId });
                        if (response.Id == null) continue;
                    }

                    // Try to make a request to the API
                    using (var httpClient = new HttpClient())
                    {
                        var response = await httpClient.PostAsync(apiUrl, new StringContent("{}"));

                        // Check if we get a response, even if it's an error
                        if (response.StatusCode != HttpStatusCode.NotFound && response.StatusCode != HttpStatusCode.Forbidden)
                        {
                            return; // API is available and responding
                        }
                    }
                }
                catch (Amazon.ApiGatewayV2.Model.NotFoundException) when (isHttpApi)
                {
                    // HTTP API not found yet, continue waiting
                }
                catch (Amazon.APIGateway.Model.NotFoundException) when (!isHttpApi)
                {
                    // REST API not found yet, continue waiting
                }
                catch (Exception ex)
                {
                    // Log unexpected exceptions
                    Console.WriteLine($"Unexpected error while checking API availability: {ex.Message}");
                }
                await Task.Delay(1000); // Wait for 1 second before checking again
            }
            throw new TimeoutException($"API {apiId} did not become available within {maxWaitTimeSeconds} seconds");
        }

        public async Task<string> AddRouteToRestApi(string restApiId, string lambdaArn, string route = "/test")
        {
            var rootResourceId = (await _apiGatewayV1Client.GetResourcesAsync(new GetResourcesRequest { RestApiId = restApiId })).Items[0].Id;

            var pathParts = route.Trim('/').Split('/');
            var currentResourceId = rootResourceId;
            foreach (var pathPart in pathParts)
            {
                var resources = await _apiGatewayV1Client.GetResourcesAsync(new GetResourcesRequest { RestApiId = restApiId });
                var existingResource = resources.Items.FirstOrDefault(r => r.ParentId == currentResourceId && r.PathPart == pathPart);

                if (existingResource == null)
                {
                    var createResourceResponse = await _apiGatewayV1Client.CreateResourceAsync(new CreateResourceRequest
                    {
                        RestApiId = restApiId,
                        ParentId = currentResourceId,
                        PathPart = pathPart
                    });
                    currentResourceId = createResourceResponse.Id;
                }
                else
                {
                    currentResourceId = existingResource.Id;
                }
            }

            await _apiGatewayV1Client.PutMethodAsync(new PutMethodRequest
            {
                RestApiId = restApiId,
                ResourceId = currentResourceId,
                HttpMethod = "ANY",
                AuthorizationType = "NONE"
            });

            await _apiGatewayV1Client.PutIntegrationAsync(new PutIntegrationRequest
            {
                RestApiId = restApiId,
                ResourceId = currentResourceId,
                HttpMethod = "ANY",
                Type = APIGateway.IntegrationType.AWS_PROXY,
                IntegrationHttpMethod = "POST",
                Uri = $"arn:aws:apigateway:{_apiGatewayV1Client.Config.RegionEndpoint.SystemName}:lambda:path/2015-03-31/functions/{lambdaArn}/invocations"
            });

            await _apiGatewayV1Client.CreateDeploymentAsync(new APIGateway.Model.CreateDeploymentRequest
            {
                RestApiId = restApiId,
                StageName = "test"
            });

            var url = $"https://{restApiId}.execute-api.{_apiGatewayV1Client.Config.RegionEndpoint.SystemName}.amazonaws.com/test{route}";
            return url;
        }

        public async Task<string> AddRouteToHttpApi(string httpApiId, string lambdaArn, string version, string route = "/test", string routeKey = "ANY")
        {
            var createIntegrationResponse = await _apiGatewayV2Client.CreateIntegrationAsync(new CreateIntegrationRequest
            {
                ApiId = httpApiId,
                IntegrationType = ApiGatewayV2.IntegrationType.AWS_PROXY,
                IntegrationUri = lambdaArn,
                PayloadFormatVersion = version
            });
            string integrationId = createIntegrationResponse.IntegrationId;

            // Split the route into parts and create each part
            var routeParts = route.Trim('/').Split('/');
            var currentPath = "";
            foreach (var part in routeParts)
            {
                currentPath += "/" + part;
                await _apiGatewayV2Client.CreateRouteAsync(new CreateRouteRequest
                {
                    ApiId = httpApiId,
                    RouteKey = $"{routeKey} {currentPath}",
                    Target = $"integrations/{integrationId}"
                });
            }

            // Create the final route (if it's not already created)
            if (currentPath != "/" + route.Trim('/'))
            {
                await _apiGatewayV2Client.CreateRouteAsync(new CreateRouteRequest
                {
                    ApiId = httpApiId,
                    RouteKey = $"{routeKey} {route}",
                    Target = $"integrations/{integrationId}"
                });
            }

            var url = $"https://{httpApiId}.execute-api.{_apiGatewayV2Client.Config.RegionEndpoint.SystemName}.amazonaws.com{route}";
            return url ;
        }


    }
}
