using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Amazon.APIGateway;
using Amazon.APIGateway.Model;
using Amazon.ApiGatewayV2;
using Amazon.ApiGatewayV2.Model;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Amazon.Lambda;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Model;
using Amazon.Lambda.TestTool.Extensions;
using Amazon.Lambda.TestTool.Models;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Amazon.Lambda.TestTool.IntegrationTests
{
    public class ApiGatewayTestHelper
    {
        private readonly IAmazonAPIGateway _apiGatewayV1Client;
        private readonly IAmazonApiGatewayV2 _apiGatewayV2Client;
        private readonly IAmazonLambda _lambdaClient;
        private readonly IAmazonIdentityManagementService _iamClient;
        private readonly HttpClient _httpClient;

        public ApiGatewayTestHelper(
            IAmazonAPIGateway apiGatewayV1Client,
            IAmazonApiGatewayV2 apiGatewayV2Client,
            IAmazonLambda lambdaClient,
            IAmazonIdentityManagementService iamClient)
        {
            _apiGatewayV1Client = apiGatewayV1Client;
            _apiGatewayV2Client = apiGatewayV2Client;
            _lambdaClient = lambdaClient;
            _iamClient = iamClient;
            _httpClient = new HttpClient();
        }

        public async Task<string> CreateLambdaFunctionAsync(string roleArn, string lambdaCode)
        {
            var functionName = $"TestFunction-{Guid.NewGuid()}";
            byte[] zipFileBytes = CreateLambdaZipPackage(lambdaCode);

            var createFunctionResponse = await _lambdaClient.CreateFunctionAsync(new CreateFunctionRequest
            {
                FunctionName = functionName,
                Handler = "index.handler",
                Role = roleArn,
                Code = new FunctionCode
                {
                    ZipFile = new MemoryStream(zipFileBytes)
                },
                Runtime = Runtime.Nodejs20X
            });

            return createFunctionResponse.FunctionArn;
        }

        public async Task GrantApiGatewayPermissionToLambda(string lambdaArn)
        {
            await _lambdaClient.AddPermissionAsync(new AddPermissionRequest
            {
                FunctionName = lambdaArn,
                StatementId = $"apigateway-test-{Guid.NewGuid()}",
                Action = "lambda:InvokeFunction",
                Principal = "apigateway.amazonaws.com"
            });
        }

        public async Task<string> CreateIamRoleAsync()
        {
            var roleName = $"TestLambdaRole-{Guid.NewGuid()}";
            var createRoleResponse = await _iamClient.CreateRoleAsync(new CreateRoleRequest
            {
                RoleName = roleName,
                AssumeRolePolicyDocument = @"{
                ""Version"": ""2012-10-17"",
                ""Statement"": [
                {
                    ""Effect"": ""Allow"",
                    ""Principal"": {
                        ""Service"": ""lambda.amazonaws.com""
                    },
                    ""Action"": ""sts:AssumeRole""
                }
            ]
        }"
            });

            await _iamClient.AttachRolePolicyAsync(new AttachRolePolicyRequest
            {
                RoleName = roleName,
                PolicyArn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
            });

            await Task.Delay(10000); // Wait for 10 seconds

            return createRoleResponse.Role.Arn;
        }

        public async Task<(string restApiId, string restApiUrl)> CreateRestApiV1(string lambdaArn)
        {
            var createRestApiResponse = await _apiGatewayV1Client.CreateRestApiAsync(new CreateRestApiRequest
            {
                Name = $"TestRestApi-{Guid.NewGuid()}"
            });
            var restApiId = createRestApiResponse.Id;

            var rootResourceId = (await _apiGatewayV1Client.GetResourcesAsync(new GetResourcesRequest { RestApiId = restApiId })).Items[0].Id;
            var createResourceResponse = await _apiGatewayV1Client.CreateResourceAsync(new CreateResourceRequest
            {
                RestApiId = restApiId,
                ParentId = rootResourceId,
                PathPart = "test"
            });
            await _apiGatewayV1Client.PutMethodAsync(new PutMethodRequest
            {
                RestApiId = restApiId,
                ResourceId = createResourceResponse.Id,
                HttpMethod = "POST",
                AuthorizationType = "NONE"
            });
            await _apiGatewayV1Client.PutIntegrationAsync(new PutIntegrationRequest
            {
                RestApiId = restApiId,
                ResourceId = createResourceResponse.Id,
                HttpMethod = "POST",
                Type = APIGateway.IntegrationType.AWS_PROXY,
                IntegrationHttpMethod = "POST",
                Uri = $"arn:aws:apigateway:{_apiGatewayV1Client.Config.RegionEndpoint.SystemName}:lambda:path/2015-03-31/functions/{lambdaArn}/invocations"
            });

            await _apiGatewayV1Client.CreateDeploymentAsync(new APIGateway.Model.CreateDeploymentRequest
            {
                RestApiId = restApiId,
                StageName = "test"
            });
            var restApiUrl = $"https://{restApiId}.execute-api.{_apiGatewayV1Client.Config.RegionEndpoint.SystemName}.amazonaws.com/test/test";

            return (restApiId, restApiUrl);
        }

        public async Task<(string httpApiId, string httpApiUrl)> CreateHttpApi(string lambdaArn, string version)
        {
            var createHttpApiResponse = await _apiGatewayV2Client.CreateApiAsync(new CreateApiRequest
            {
                ProtocolType = ProtocolType.HTTP,
                Name = $"TestHttpApi-{Guid.NewGuid()}",
                Version = version
            });
            var httpApiId = createHttpApiResponse.ApiId;

            var createIntegrationResponse = await _apiGatewayV2Client.CreateIntegrationAsync(new CreateIntegrationRequest
            {
                ApiId = httpApiId,
                IntegrationType = ApiGatewayV2.IntegrationType.AWS_PROXY,
                IntegrationUri = lambdaArn,
                PayloadFormatVersion = version
            });
            string integrationId = createIntegrationResponse.IntegrationId;

            await _apiGatewayV2Client.CreateRouteAsync(new CreateRouteRequest
            {
                ApiId = httpApiId,
                RouteKey = "POST /test",
                Target = $"integrations/{integrationId}"
            });

            await _apiGatewayV2Client.CreateStageAsync(new ApiGatewayV2.Model.CreateStageRequest
            {
                ApiId = httpApiId,
                StageName = "$default",
                AutoDeploy = true
            });

            var httpApiUrl = $"https://{httpApiId}.execute-api.{_apiGatewayV2Client.Config.RegionEndpoint.SystemName}.amazonaws.com/test";

            return (httpApiId, httpApiUrl);
        }

        private byte[] CreateLambdaZipPackage(string lambdaCode)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
                {
                    var fileInArchive = archive.CreateEntry("index.js", CompressionLevel.Optimal);
                    using (var entryStream = fileInArchive.Open())
                    using (var streamWriter = new StreamWriter(entryStream))
                    {
                        streamWriter.Write(lambdaCode);
                    }
                }
                return memoryStream.ToArray();
            }
        }

        public async Task<(HttpResponseMessage actualResponse, HttpResponse httpTestResponse)> ExecuteTestRequest(APIGatewayProxyResponse testResponse, string apiUrl, ApiGatewayEmulatorMode emulatorMode)
        {
            var httpTestResponse = testResponse.ToHttpResponse(emulatorMode);
            var serialized = JsonSerializer.Serialize(testResponse);
            var actualResponse = await _httpClient.PostAsync(apiUrl, new StringContent(serialized));
            return (actualResponse, httpTestResponse);
        }

        public async Task<(HttpResponseMessage actualResponse, HttpResponse httpTestResponse)> ExecuteTestRequest(APIGatewayHttpApiV2ProxyResponse testResponse, string apiUrl)
        {
            var httpTestResponse = testResponse.ToHttpResponse();
            var serialized = JsonSerializer.Serialize(testResponse);
            var actualResponse = await _httpClient.PostAsync(apiUrl, new StringContent(serialized));
            return (actualResponse, httpTestResponse);
        }

        public async Task AssertResponsesEqual(HttpResponseMessage actualResponse, HttpResponse httpTestResponse)
        {

            var expectedContent = await new StreamReader(httpTestResponse.Body).ReadToEndAsync();
            httpTestResponse.Body.Seek(0, SeekOrigin.Begin);
            var actualContent = await actualResponse.Content.ReadAsStringAsync();

            Assert.Equal(expectedContent, actualContent);

            Assert.Equal(httpTestResponse.StatusCode, (int)actualResponse.StatusCode);

            // ignore these because they will vary in the real world. we will check manually in other test cases that these are set
            var headersToIgnore = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Date",
                "Apigw-Requestid",
                "X-Amzn-Trace-Id", 
                "x-amzn-RequestId",
                "x-amz-apigw-id", 
                "X-Cache",
                "Via", 
                "X-Amz-Cf-Pop", 
                "X-Amz-Cf-Id"
            };

            foreach (var header in httpTestResponse.Headers)
            {
                if (headersToIgnore.Contains(header.Key)) continue;

                Assert.True(actualResponse.Headers.TryGetValues(header.Key, out var actualValues) ||
                            actualResponse.Content.Headers.TryGetValues(header.Key, out actualValues),
                            $"Header '{header.Key}={string.Join(", ", header.Value)}' not found in actual response");

                var sortedExpectedValues = header.Value.OrderBy(v => v).ToArray();
                var sortedActualValues = actualValues.OrderBy(v => v).ToArray();
                Assert.Equal(sortedExpectedValues, sortedActualValues);
            }

            foreach (var header in actualResponse.Headers.Concat(actualResponse.Content.Headers))
            {
                if (headersToIgnore.Contains(header.Key)) continue;

                Assert.True(httpTestResponse.Headers.ContainsKey(header.Key),
                            $"Header '{header.Key}={string.Join(", ", header.Value)}' not found in test response");

                var sortedExpectedValues = httpTestResponse.Headers[header.Key].OrderBy(v => v).ToArray();
                var sortedActualValues = header.Value.OrderBy(v => v).ToArray();
                Assert.Equal(sortedExpectedValues, sortedActualValues);
            }
        }

        public async Task CleanupResources(string restApiId, string httpApiV1Id, string httpApiV2Id, string lambdaArn, string roleArn)
        {
            if (!string.IsNullOrEmpty(restApiId))
                await _apiGatewayV1Client.DeleteRestApiAsync(new DeleteRestApiRequest { RestApiId = restApiId });

            if (!string.IsNullOrEmpty(httpApiV1Id))
                await _apiGatewayV2Client.DeleteApiAsync(new DeleteApiRequest { ApiId = httpApiV1Id });

            if (!string.IsNullOrEmpty(httpApiV2Id))
                await _apiGatewayV2Client.DeleteApiAsync(new DeleteApiRequest { ApiId = httpApiV2Id });

            if (!string.IsNullOrEmpty(lambdaArn))
                await _lambdaClient.DeleteFunctionAsync(new DeleteFunctionRequest { FunctionName = lambdaArn });

            if (!string.IsNullOrEmpty(roleArn))
            {
                var roleName = roleArn.Split('/').Last();
                var attachedPolicies = await _iamClient.ListAttachedRolePoliciesAsync(new ListAttachedRolePoliciesRequest { RoleName = roleName });
                foreach (var policy in attachedPolicies.AttachedPolicies)
                {
                    await _iamClient.DetachRolePolicyAsync(new DetachRolePolicyRequest
                    {
                        RoleName = roleName,
                        PolicyArn = policy.PolicyArn
                    });
                }
                await _iamClient.DeleteRoleAsync(new DeleteRoleRequest { RoleName = roleName });
            }
        }
    }
}
