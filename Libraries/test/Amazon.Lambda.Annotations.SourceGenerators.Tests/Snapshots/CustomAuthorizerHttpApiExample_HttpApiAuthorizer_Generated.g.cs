using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class CustomAuthorizerHttpApiExample_HttpApiAuthorizer_Generated
    {
        private readonly CustomAuthorizerHttpApiExample customAuthorizerHttpApiExample;

        public CustomAuthorizerHttpApiExample_HttpApiAuthorizer_Generated()
        {
            SetExecutionEnvironment();
            customAuthorizerHttpApiExample = new CustomAuthorizerHttpApiExample();
        }

        public async System.Threading.Tasks.Task<Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse> HttpApiAuthorizer(Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest __request__, Amazon.Lambda.Core.ILambdaContext __context__)
        {
            var validationErrors = new List<string>();

            var authorizerValue = __request__.RequestContext.Authorizer.Lambda["authKey"].ToString();
            // return 400 Bad Request if there exists a validation error
            if (validationErrors.Any())
            {
                var errorResult = new Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse
                {
                    Body = @$"{{""message"": ""{validationErrors.Count} validation error(s) detected: {string.Join(",", validationErrors)}""}}",
                    Headers = new Dictionary<string, string>
                    {
                        {"Content-Type", "application/json"},
                        {"x-amzn-ErrorType", "ValidationException"}
                    },
                    StatusCode = 400
                };
                return errorResult;
            }

            await customAuthorizerHttpApiExample.HttpApiAuthorizer(authorizerValue, __context__);

            return new Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse
            {
                StatusCode = 200
            };
        }

        private static void SetExecutionEnvironment()
        {
            const string envName = "AWS_EXECUTION_ENV";

            var envValue = new StringBuilder();

            // If there is an existing execution environment variable add the annotations package as a suffix.
            if(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(envName)))
            {
                envValue.Append($"{Environment.GetEnvironmentVariable(envName)}_");
            }

            envValue.Append("amazon-lambda-annotations_0.13.0.0");

            Environment.SetEnvironmentVariable(envName, envValue.ToString());
        }
    }
}