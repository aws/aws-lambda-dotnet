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

        [LambdaFunction(Name = "SimpleCalculatorAdd")]
        [RestApi]
        public int Add([FromQuery]int x, [FromQuery]int y)
        {
            return _simpleCalculatorService.Add(x, y);
        }

        [LambdaFunction(Name = "SimpleCalculatorSubtract")]
        [RestApi]
        public APIGatewayProxyResponse Subtract([FromServices]ISimpleCalculatorService simpleCalculatorService)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = simpleCalculatorService.Subtract(4, 2).ToString()
            };
        }

        [LambdaFunction(Name = "SimpleCalculatorMultiply")]
        [RestApi]
        public string Multiply()
        {
            return _simpleCalculatorService.Multiply(4, 2).ToString();
        }

        [LambdaFunction(Name = "SimpleCalculatorDivideAsync")]
        [RestApi]
        public async Task<int> DivideAsync()
        {
            return await Task.FromResult(_simpleCalculatorService.Divide(4, 2));
        }
    }
}