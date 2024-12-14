// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using Amazon.APIGateway;
using Amazon.ApiGatewayV2;
using Amazon.APIGateway.Model;
using Amazon.ApiGatewayV2.Model;
using System.Net;

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
                        if (response.StatusCode != HttpStatusCode.NotFound)
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
    }
}
