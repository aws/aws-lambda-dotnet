using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Annotations;
using System.Runtime.InteropServices;
using TestServerlessApp.Services;
using System.Numerics;

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


        /// <summary>
        /// A Lambda function to respond to HTTP Get methods from API Gateway
        /// </summary>
        /// <param name="request"></param>
        /// <returns>The API Gateway response.</returns>
        public APIGatewayProxyResponse Get(APIGatewayProxyRequest request, ILambdaContext context)
        {
            context.Logger.LogLine("Get Request\n");

            var response = new APIGatewayProxyResponse
            {
                StatusCode = (int)HttpStatusCode.OK,
                Body = "Hello AWS Serverless",
                Headers = new Dictionary<string, string> { { "Content-Type", "text/plain" } }
            };

            return response;
        }

        [LambdaFunction]
        public int Plus()
        {

            return _simpleCalculatorService.Plus(4, 2);
        }

        [LambdaFunction]
        public APIGatewayProxyResponse Subtract()
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = _simpleCalculatorService.Subtract(4, 2).ToString()
            };
        }

        [LambdaFunction]
        public string Multiply()
        {
            return _simpleCalculatorService.Multiply(4, 2).ToString();
        }

        [LambdaFunction]
        public async Task<int> Divide()
        {
            return await Task.FromResult(_simpleCalculatorService.Divide(4, 2));
        }
    }
}
