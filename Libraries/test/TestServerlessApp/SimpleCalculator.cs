using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Annotations;
using TestServerlessApp.Services;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace TestServerlessApp
{
    public class SimpleCalculator
    {
        private readonly ISimpleCalculatorService _simpleCalculatorService;

        /// <summary>
        /// Default constructor that Lambda will invoke.
        /// </summary>
        public SimpleCalculator(ISimpleCalculatorService simpleCalculatorService)
        {
            this._simpleCalculatorService = simpleCalculatorService;
        }

        [LambdaFunction]
        [APIRoute]
        public int Add()
        {
            return _simpleCalculatorService.Add(4, 2);
        }

        [LambdaFunction]
        [APIRoute]
        public APIGatewayProxyResponse Subtract([FromServices]ISimpleCalculatorService simpleCalculatorService)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = simpleCalculatorService.Subtract(4, 2).ToString()
            };
        }

        [LambdaFunction]
        [APIRoute]

        public string Multiply()
        {
            return _simpleCalculatorService.Multiply(4, 2).ToString();
        }

        [LambdaFunction]
        [APIRoute]
        public async Task<int> DivideAsync()
        {
            return await Task.FromResult(_simpleCalculatorService.Divide(4, 2));
        }
    }
}