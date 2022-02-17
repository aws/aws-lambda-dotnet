using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class Greeter_SayHelloAsync_Generated
    {
        private readonly Greeter greeter;

        public Greeter_SayHelloAsync_Generated()
        {
            SetExecutionEnvironment();
            greeter = new Greeter();
        }

        public async System.Threading.Tasks.Task<Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse> SayHelloAsync(Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest request, Amazon.Lambda.Core.ILambdaContext context)
        {
            var validationErrors = new List<string>();

            var firstNames = default(System.Collections.Generic.IEnumerable<string>);
            if (request.MultiValueHeaders?.ContainsKey("names") == true)
            {
                firstNames = request.MultiValueHeaders["names"]
                    .Select(q =>
                    {
                        try
                        {
                            return (string)Convert.ChangeType(q, typeof(string));
                        }
                        catch (Exception e) when (e is InvalidCastException || e is FormatException || e is OverflowException || e is ArgumentException)
                        {
                        validationErrors.Add($"Value {q} at 'names' failed to satisfy constraint: {e.Message}");
                            return default;
                        }
                    })
                    .ToList();
            }

            // return 400 Bad Request if there exists a validation error
            if (validationErrors.Any())
            {
                return new Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse
                {
                    Body = @$"{{""message"": ""{validationErrors.Count} validation error(s) detected: {string.Join(",", validationErrors)}""}}",
                    Headers = new Dictionary<string, string>
                    {
                        {"Content-Type", "application/json"},
                        {"x-amzn-ErrorType", "ValidationException"}
                    },
                    StatusCode = 400
                };
            }

            await greeter.SayHelloAsync(firstNames);

            return new Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse
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

            envValue.Append("amazon-lambda-annotations_0.5.0.0");

            Environment.SetEnvironmentVariable(envName, envValue.ToString());
        }
    }
}