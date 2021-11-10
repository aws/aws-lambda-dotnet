using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Lambda;
using Amazon.Lambda.Model;

namespace TestServerlessApp.IntegrationTests.Helpers
{
    public class LambdaHelper
    {
        private readonly IAmazonLambda _lambdaClient;

        public LambdaHelper(IAmazonLambda lambdaClient)
        {
            _lambdaClient = lambdaClient;
        }

        public async Task<List<LambdaFunction>> FilterByCloudFormationStack(string stackName)
        {
            const string stackNameKey = "aws:cloudformation:stack-name";
            const string logicalIdKey = "aws:cloudformation:logical-id";
            var lambdaFunctions = new List<LambdaFunction>();
            var response = await _lambdaClient.ListFunctionsAsync(new ListFunctionsRequest());

            foreach (var function in response.Functions)
            {
                var tags = (await _lambdaClient.ListTagsAsync(new ListTagsRequest {Resource = function.FunctionArn})).Tags;
                if (tags.ContainsKey(stackNameKey) && string.Equals(tags[stackNameKey], stackName))
                {
                    var lambdaFunction = new LambdaFunction
                    {
                        LogicalId = tags[logicalIdKey],
                        Name = function.FunctionName
                    };
                    lambdaFunctions.Add(lambdaFunction);
                }
            }

            return lambdaFunctions;
        }

        public async Task<InvokeResponse> InvokeFunction(string functionName, string payload = null)
        {
            var request = new InvokeRequest {FunctionName = functionName};
            if (!string.IsNullOrEmpty(payload))
                request.Payload = payload;
            return await _lambdaClient.InvokeAsync(request);
        }
    }

    public class LambdaFunction
    {
        public string LogicalId { get; set; }
        public string Name { get; set; }
    }
}