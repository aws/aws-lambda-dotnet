using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class NullableReferenceTypeExample_NullableHeaderHttpApi_Generated
    {
        private readonly NullableReferenceTypeExample nullableReferenceTypeExample;
        private readonly Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer serializer;

        public NullableReferenceTypeExample_NullableHeaderHttpApi_Generated()
        {
            SetExecutionEnvironment();
            nullableReferenceTypeExample = new NullableReferenceTypeExample();
            serializer = new Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer();
        }

        public Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyResponse NullableHeaderHttpApi(Amazon.Lambda.APIGatewayEvents.APIGatewayHttpApiV2ProxyRequest __request__, Amazon.Lambda.Core.ILambdaContext __context__)
        {
            var validationErrors = new List<string>();

            var text = default(string?);
            if (__request__.Headers?.Any(x => string.Equals(x.Key, "MyHeader", StringComparison.OrdinalIgnoreCase)) == true)
            {
                try
                {
                    text = (string?)Convert.ChangeType(__request__.Headers.First(x => string.Equals(x.Key, "MyHeader", StringComparison.OrdinalIgnoreCase)).Value, typeof(string));
                }
                catch (Exception e) when (e is InvalidCastException || e is FormatException || e is OverflowException || e is ArgumentException)
                {
                    validationErrors.Add($"Value {__request__.Headers.First(x => string.Equals(x.Key, "MyHeader", StringComparison.OrdinalIgnoreCase)).Value} at 'MyHeader' failed to satisfy constraint: {e.Message}");
                }
            }

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

            nullableReferenceTypeExample.NullableHeaderHttpApi(text, __context__);

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

            envValue.Append("amazon-lambda-annotations_1.2.0.0");

            Environment.SetEnvironmentVariable(envName, envValue.ToString());
        }
    }
}