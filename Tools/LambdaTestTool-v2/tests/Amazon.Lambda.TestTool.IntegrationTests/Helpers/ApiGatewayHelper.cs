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
        private readonly IAmazonAPIGateway _apiGateway;
        private readonly IAmazonApiGatewayV2 _apiGatewayV2;
        private readonly HttpClient _httpClient;

        public ApiGatewayHelper(IAmazonAPIGateway apiGateway, IAmazonApiGatewayV2 apiGatewayV2)
        {
            _apiGateway = apiGateway;
            _apiGatewayV2 = apiGatewayV2;
            _httpClient = new HttpClient();
        }

        public async Task WaitForApiAvailability(string apiId, string apiUrl, bool isHttpApi, int maxWaitTimeSeconds = 60)
        {
            var startTime = DateTime.UtcNow;
            var successStartTime = DateTime.UtcNow;
            var requiredSuccessDuration = TimeSpan.FromSeconds(10);
            bool hasBeenSuccessful = false;

            while ((DateTime.UtcNow - startTime).TotalSeconds < maxWaitTimeSeconds)
            {
                try
                {
                    // Check if the API exists
                    if (isHttpApi)
                    {
                        var response = await _apiGatewayV2.GetApiAsync(new GetApiRequest { ApiId = apiId });
                        if (response.ApiEndpoint == null) continue;
                    }
                    else
                    {
                        var response = await _apiGateway.GetRestApiAsync(new GetRestApiRequest { RestApiId = apiId });
                        if (response.Id == null) continue;
                    }

                    // Try to make a request to the API
                    using (var httpClient = new HttpClient())
                    {
                        var response = await httpClient.PostAsync(apiUrl, new StringContent("{}"));

                        // Check if we get a successful response
                        if (response.StatusCode != HttpStatusCode.Forbidden && response.StatusCode != HttpStatusCode.NotFound)
                        {
                            if (!hasBeenSuccessful)
                            {
                                successStartTime = DateTime.UtcNow;
                                hasBeenSuccessful = true;
                            }

                            if ((DateTime.UtcNow - successStartTime) >= requiredSuccessDuration)
                            {
                                return; // API has been responding successfully for at least 10 seconds
                            }
                        }
                        else
                        {
                            // Reset the success timer if we get a non-successful response
                            hasBeenSuccessful = false;
                            Console.WriteLine($"API responded with status code: {response.StatusCode}");
                        }
                    }
                }
                catch (Amazon.ApiGatewayV2.Model.NotFoundException) when (isHttpApi)
                {
                    // HTTP API not found yet, continue waiting
                    hasBeenSuccessful = false;
                }
                catch (Amazon.APIGateway.Model.NotFoundException) when (!isHttpApi)
                {
                    // REST API not found yet, continue waiting
                    hasBeenSuccessful = false;
                }
                catch (Exception ex)
                {
                    // Log unexpected exceptions and reset success timer
                    Console.WriteLine($"Unexpected error while checking API availability: {ex.Message}");
                    hasBeenSuccessful = false;
                }
                await Task.Delay(1000); // Wait for 1 second before checking again
            }
            throw new TimeoutException($"API {apiId} did not become consistently available within {maxWaitTimeSeconds} seconds");
        }

        public async Task<string> AddRouteToRestApi(string apiId, string lambdaArn, string path, string[]? binaryMediaTypes = null)
        {
            // Create resource
            var createResourceRequest = new CreateResourceRequest
            {
                RestApiId = apiId,
                ParentId = await GetRootResourceId(apiId),
                PathPart = path.TrimStart('/')
            };
            var resource = await _apiGateway.CreateResourceAsync(createResourceRequest);

            // Create method
            var putMethodRequest = new PutMethodRequest
            {
                RestApiId = apiId,
                ResourceId = resource.Id,
                HttpMethod = "POST",
                AuthorizationType = "NONE"
            };
            await _apiGateway.PutMethodAsync(putMethodRequest);

            // Create integration
            var putIntegrationRequest = new PutIntegrationRequest
            {
                RestApiId = apiId,
                ResourceId = resource.Id,
                HttpMethod = "POST",
                Type = "AWS_PROXY",
                IntegrationHttpMethod = "POST",
                Uri = lambdaArn
            };
            await _apiGateway.PutIntegrationAsync(putIntegrationRequest);

            // If binary media types are specified, update the API
            if (binaryMediaTypes != null && binaryMediaTypes.Length > 0)
            {
                var updateRestApiRequest = new UpdateRestApiRequest
                {
                    RestApiId = apiId,
                    PatchOperations = new List<PatchOperation>
                    {
                        new PatchOperation
                        {
                            Op = "replace",
                            Path = "/binaryMediaTypes",
                            Value = string.Join(",", binaryMediaTypes)
                        }
                    }
                };
                await _apiGateway.UpdateRestApiAsync(updateRestApiRequest);
            }

            return resource.Id;
        }

        public async Task DeleteRouteFromRestApi(string apiId, string resourceId)
        {
            try
            {
                // Delete the resource
                var deleteResourceRequest = new DeleteResourceRequest
                {
                    RestApiId = apiId,
                    ResourceId = resourceId
                };
                await _apiGateway.DeleteResourceAsync(deleteResourceRequest);
            }
            catch (Exception ex)
            {
                // Log the error but don't throw to ensure cleanup continues
                Console.WriteLine($"Error deleting REST API route: {ex.Message}");
            }
        }

        public async Task<string> AddRouteToHttpApi(string apiId, string lambdaArn, string payloadFormatVersion, string path, string httpMethod)
        {
            // Create integration
            var createIntegrationRequest = new CreateIntegrationRequest
            {
                ApiId = apiId,
                IntegrationType = "AWS_PROXY",
                IntegrationUri = lambdaArn,
                PayloadFormatVersion = payloadFormatVersion
            };
            var integration = await _apiGatewayV2.CreateIntegrationAsync(createIntegrationRequest);

            // Create route
            var createRouteRequest = new CreateRouteRequest
            {
                ApiId = apiId,
                RouteKey = $"{httpMethod} {path}",
                Target = $"integrations/{integration.IntegrationId}"
            };
            var route = await _apiGatewayV2.CreateRouteAsync(createRouteRequest);

            return route.RouteId;
        }

        public async Task DeleteRouteFromHttpApi(string apiId, string routeId)
        {
            try
            {
                // Delete the route
                var deleteRouteRequest = new DeleteRouteRequest
                {
                    ApiId = apiId,
                    RouteId = routeId
                };
                await _apiGatewayV2.DeleteRouteAsync(deleteRouteRequest);
            }
            catch (Exception ex)
            {
                // Log the error but don't throw to ensure cleanup continues
                Console.WriteLine($"Error deleting HTTP API route: {ex.Message}");
            }
        }

        private async Task<string> GetRootResourceId(string apiId)
        {
            var resources = await _apiGateway.GetResourcesAsync(new GetResourcesRequest { RestApiId = apiId });
            return resources.Items.First(r => r.Path == "/").Id;
        }
    }
}
