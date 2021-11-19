using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

namespace TestServerlessApp
{
    public class Greeter_SayHello_Generated
    {
        private readonly Greeter greeter;

        public Greeter_SayHello_Generated()
        {
            SetExecutionEnvironment();
            greeter = new Greeter();
        }

        public Amazon.Lambda.APIGatewayEvents.APIGatewayProxyResponse SayHello(Amazon.Lambda.APIGatewayEvents.APIGatewayProxyRequest request, Amazon.Lambda.Core.ILambdaContext context)
        {
            var firstNames = default(System.Collections.Generic.IEnumerable<string>);
            if (request.MultiValueQueryStringParameters?.ContainsKey("names") == true)
            {
                firstNames = request.MultiValueQueryStringParameters["names"].Select(q => (string)Convert.ChangeType(q, typeof(string))).ToList();
            }

            greeter.SayHello(firstNames, request, context);

            return new APIGatewayProxyResponse
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

            envValue.Append("amazon-lambda-annotations_0.1.0.0");

            Environment.SetEnvironmentVariable(envName, envValue.ToString());
        }
    }
}