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
        [RestApi(HttpMethod.Get, "/SimpleCalculator/Add")]
        public int Add([FromQuery]int x, [FromQuery]int y)
        {
            return _simpleCalculatorService.Add(x, y);
        }

        [LambdaFunction(Name = "SimpleCalculatorSubtract")]
        [RestApi(HttpMethod.Get, "/SimpleCalculator/Subtract")]
        public APIGatewayProxyResponse Subtract([FromHeader]int x, [FromHeader]int y, [FromServices]ISimpleCalculatorService simpleCalculatorService)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = simpleCalculatorService.Subtract(x, y).ToString()
            };
        }

        [LambdaFunction(Name = "SimpleCalculatorMultiply")]
        [RestApi(HttpMethod.Get, "/SimpleCalculator/Multiply/{x}/{y}")]
        public string Multiply(int x, int y)
        {
            return _simpleCalculatorService.Multiply(x, y).ToString();
        }

        [LambdaFunction(Name = "SimpleCalculatorDivideAsync")]
        [RestApi(template: "/SimpleCalculator/DivideAsync/{x}/{y}", method: HttpMethod.Get)]
        public async Task<int> DivideAsync([FromRoute(Name = "x")]int first, [FromRoute(Name = "y")]int second)
        {
            return await Task.FromResult(_simpleCalculatorService.Divide(first, second));
        }
    }
}