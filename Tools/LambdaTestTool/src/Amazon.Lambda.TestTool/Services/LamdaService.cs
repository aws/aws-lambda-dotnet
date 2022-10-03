using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.TestTool.Runtime;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Amazon.Lambda.TestTool.Services
{
    public class LamdaService : ILamdaService
    {
        private readonly LocalLambdaOptions _options;
        private readonly IAwsProfileConfig _awsProfileConfig;

        public LamdaService(LocalLambdaOptions options, IAwsProfileConfig awsProfileConfig)
        {
            _options = options;
            _awsProfileConfig = awsProfileConfig;
        }

        public async Task<ExecutionResponse> Execute(string functionName, HttpContext context, IDictionary<string, string> pathParameters)
        {
            var awsProfile = "default";
            var awsRegion = string.Empty;

            var parameters = context.Request.Query;

            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();

            var json = body;

            var configFile = _awsProfileConfig.ConfigFile();
            var lamdaConfigInfo = _awsProfileConfig.LambdaConfigInfo();

            var availableAwsProfile = _awsProfileConfig.AvailableAWSProfiles();

            var availableFunctions = _awsProfileConfig.AvailableFunctions();
            if (lamdaConfigInfo.AWSProfile != null && availableAwsProfile.Contains(lamdaConfigInfo.AWSProfile))
            {
                awsProfile = lamdaConfigInfo.AWSProfile;
            }

            if (!string.IsNullOrEmpty(lamdaConfigInfo.AWSRegion))
            {
                awsRegion = lamdaConfigInfo.AWSRegion;
            };

            var dictParameters = context.Request.Query.ToDictionary(item => item.Key.ToString(), item => item.Value.ToString());
            var headers = context.Request.Headers.ToDictionary(item => item.Key.ToString(), item => item.Value.ToString());

            var payload = GetPayload(pathParameters, dictParameters, json, headers, context.Request.Method);

            var function = this._options.LoadLambdaFuntion(configFile, functionName);
            var request = new ExecutionRequest()
            {
                Function = function,
                AWSProfile = awsProfile,
                AWSRegion = awsRegion,
                Payload = payload
            };
            var response = await _options.LambdaRuntime.ExecuteLambdaFunctionAsync(request);

            return response;
        }

        private string GetPayload(IDictionary<string, string> pathParameters, IDictionary<string, string> queryParameters, string json, IDictionary<string, string> headers, string httpMethod)
        {
            //APIGatewayProxyRequest APIGatewayProxyResponse
            var authorizer = new APIGatewayCustomAuthorizerContext();
            authorizer.Add("principalId", 3);
            var request = new APIGatewayProxyRequest
            {
                Body = json,
                Headers = headers,
                HttpMethod = httpMethod,
                PathParameters = pathParameters,
                Path = "/path/to/resource",
                QueryStringParameters = queryParameters,
                RequestContext = new APIGatewayProxyRequest.ProxyRequestContext
                {
                    Authorizer = authorizer
                }
            };
            var response = JsonConvert.SerializeObject(request);
            return response;
        }
    }
}
