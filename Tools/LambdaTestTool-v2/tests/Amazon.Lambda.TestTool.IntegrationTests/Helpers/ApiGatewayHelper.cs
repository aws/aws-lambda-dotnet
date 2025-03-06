// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.APIGateway;
using Amazon.ApiGatewayV2;
using Amazon.APIGateway.Model;
using Amazon.ApiGatewayV2.Model;
using System.Net;
using Amazon.Runtime.Internal.Endpoints.StandardLibrary;
using ConflictException = Amazon.ApiGatewayV2.Model.ConflictException;

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


        public async Task<string> AddRouteToRestApi(string restApiId, string lambdaArn, string route = "/test", string httpMethod = "ANY")
        {
            // Get all resources and find the root resource
            var resources = await _apiGatewayV1Client.GetResourcesAsync(new GetResourcesRequest { RestApiId = restApiId });
            var rootResource = resources.Items.First(r => r.Path == "/");
            var rootResourceId = rootResource.Id;

            // Split the route into parts and create each part
            var routeParts = route.Trim('/').Split('/');
            string currentPath = "";
            string parentResourceId = rootResourceId;

            foreach (var part in routeParts)
            {
                currentPath += "/" + part;

                // Check if the resource already exists
                var existingResource = resources.Items.FirstOrDefault(r => r.Path == currentPath);
                if (existingResource == null)
                {
                    // Create the resource if it doesn't exist
                    var createResourceResponse = await _apiGatewayV1Client.CreateResourceAsync(new CreateResourceRequest
                    {
                        RestApiId = restApiId,
                        ParentId = parentResourceId,
                        PathPart = part
                    });
                    parentResourceId = createResourceResponse.Id;
                }
                else
                {
                    parentResourceId = existingResource.Id;
                }
            }

            // Create the method and integration
            try 
            {
                await _apiGatewayV1Client.PutMethodAsync(new PutMethodRequest
                {
                    RestApiId = restApiId,
                    ResourceId = parentResourceId,
                    HttpMethod = httpMethod,
                    AuthorizationType = "NONE"
                });

                await _apiGatewayV1Client.PutIntegrationAsync(new PutIntegrationRequest
                {
                    RestApiId = restApiId,
                    ResourceId = parentResourceId,
                    HttpMethod = httpMethod,
                    Type = Amazon.APIGateway.IntegrationType.AWS_PROXY,
                    IntegrationHttpMethod = "POST",
                    Uri = $"arn:aws:apigateway:{_apiGatewayV1Client.Config.RegionEndpoint.SystemName}:lambda:path/2015-03-31/functions/{lambdaArn}/invocations"
                });
            }
            catch (Amazon.APIGateway.Model.ConflictException)
            {
                // Integration already exists, continue
            }

            // Create and wait for deployment
            var deploymentResponse = await _apiGatewayV1Client.CreateDeploymentAsync(new Amazon.APIGateway.Model.CreateDeploymentRequest
            {
                RestApiId = restApiId,
                StageName = "test"
            });

            return $"https://{restApiId}.execute-api.{_apiGatewayV1Client.Config.RegionEndpoint.SystemName}.amazonaws.com/test{route}";
        }

        public async Task<string> AddRouteToHttpApi(string httpApiId, string lambdaArn, string version, string route = "/test", string httpMethod = "ANY")
        {
            var createIntegrationResponse = await _apiGatewayV2Client.CreateIntegrationAsync(new CreateIntegrationRequest
            {
                ApiId = httpApiId,
                IntegrationType = Amazon.ApiGatewayV2.IntegrationType.AWS_PROXY,
                IntegrationUri = lambdaArn,
                PayloadFormatVersion = version,
                IntegrationMethod = "POST"
            });
            string integrationId = createIntegrationResponse.IntegrationId;

            // Split the route into parts and create each part
            var routeParts = route.Trim('/').Split('/');
            var currentPath = "";
            foreach (var part in routeParts)
            {
                currentPath += "/" + part;
                try
                {
                    await _apiGatewayV2Client.CreateRouteAsync(new CreateRouteRequest
                    {
                        ApiId = httpApiId,
                        RouteKey = $"{httpMethod} {currentPath}",
                        Target = $"integrations/{integrationId}"
                    });
                }
                catch (ConflictException)
                {
                    // ignore route already exists
                }
               
            }

            // Create the final route (if it's not already created)
            if (currentPath != "/" + route.Trim('/'))
            {
                try
                {
                    await _apiGatewayV2Client.CreateRouteAsync(new CreateRouteRequest
                    {
                        ApiId = httpApiId,
                        RouteKey = $"{httpMethod} {route}",
                        Target = $"integrations/{integrationId}"
                    });
                }
                catch(ConflictException)
                {
                    // ignore
                }
               
            }

            // Create and wait for deployment
            var deployment = await _apiGatewayV2Client.CreateDeploymentAsync(new Amazon.ApiGatewayV2.Model.CreateDeploymentRequest
            {
                ApiId = httpApiId
            });

            // Create stage if it doesn't exist
            try 
            {
                await _apiGatewayV2Client.CreateStageAsync(new Amazon.ApiGatewayV2.Model.CreateStageRequest
                {
                    ApiId = httpApiId,
                    StageName = "$default",
                    AutoDeploy = true
                });
            }
            catch (Amazon.ApiGatewayV2.Model.ConflictException)
            {
                // Stage already exists, continue
            }

            var url = $"https://{httpApiId}.execute-api.{_apiGatewayV2Client.Config.RegionEndpoint.SystemName}.amazonaws.com{route}";
            return url ;
        }


    }
}
