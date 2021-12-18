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

        public async Task<List<LambdaFunction>> FilterByCloudFormationStackAsync(string stackName)
        {
            const string stackNameKey = "aws:cloudformation:stack-name";
            const string logicalIdKey = "aws:cloudformation:logical-id";
            var lambdaFunctions = new List<LambdaFunction>();
            var paginator = _lambdaClient.Paginators.ListFunctions(new ListFunctionsRequest());

            await foreach (var function in paginator.Functions)
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

        public async Task<InvokeResponse> InvokeFunctionAsync(string functionName, string payload = null)
        {
            var request = new InvokeRequest {FunctionName = functionName};
            if (!string.IsNullOrEmpty(payload))
                request.Payload = payload;
            return await _lambdaClient.InvokeAsync(request);
        }

        public async Task WaitTillNotPending(List<string> functions)
        {
            foreach(var function in functions)
            {
                while(true)
                {
                    var response = await _lambdaClient.GetFunctionConfigurationAsync(new GetFunctionConfigurationRequest {FunctionName = function });
                    if(response.State == State.Pending)
                    {
                        await Task.Delay(1000);
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }
    }

    public class LambdaFunction
    {
        public string LogicalId { get; set; }
        public string Name { get; set; }
    }
}