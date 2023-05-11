using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using TestServerlessApp.Services;

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

        [LambdaFunction(ResourceName = "SimpleCalculatorAdd", PackageType = LambdaPackageType.Image)]
        [RestApi(LambdaHttpMethod.Get, "/SimpleCalculator/Add")]
        public int Add([FromQuery]int x, [FromQuery]int y)
        {
            return _simpleCalculatorService.Add(x, y);
        }

        [LambdaFunction(ResourceName = "SimpleCalculatorSubtract", PackageType = LambdaPackageType.Image)]
        [RestApi(LambdaHttpMethod.Get, "/SimpleCalculator/Subtract")]
        public APIGatewayProxyResponse Subtract([FromHeader]int x, [FromHeader]int y, [FromServices]ISimpleCalculatorService simpleCalculatorService)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = simpleCalculatorService.Subtract(x, y).ToString()
            };
        }

        [LambdaFunction(ResourceName = "SimpleCalculatorMultiply", PackageType = LambdaPackageType.Image)]
        [RestApi(LambdaHttpMethod.Get, "/SimpleCalculator/Multiply/{x}/{y}")]
        public string Multiply(int x, int y)
        {
            return _simpleCalculatorService.Multiply(x, y).ToString();
        }

        [LambdaFunction(ResourceName = "SimpleCalculatorDivideAsync", PackageType = LambdaPackageType.Image)]
        [RestApi(template: "/SimpleCalculator/DivideAsync/{x}/{y}", method: LambdaHttpMethod.Get)]
        public async Task<int> DivideAsync([FromRoute(Name = "x")]int first, [FromRoute(Name = "y")]int second)
        {
            return await Task.FromResult(_simpleCalculatorService.Divide(first, second));
        }

        [LambdaFunction(ResourceName = "PI", PackageType = LambdaPackageType.Image)]
        public double Pi([FromServices]ISimpleCalculatorService simpleCalculatorService)
        {
            return simpleCalculatorService.PI();
        }

        [LambdaFunction(ResourceName = "Random", PackageType = LambdaPackageType.Image)]
        public async Task<int> Random(int maxValue, ILambdaContext context)
        {
            context.Logger.Log($"Max value: {maxValue}");
            var value = new Random().Next(maxValue);
            return await Task.FromResult(value);
        }

        [LambdaFunction(ResourceName = "Randoms", PackageType = LambdaPackageType.Image)]
        public IList<int> Randoms(RandomsInput input, ILambdaContext context)
        {
            context.Logger.Log($"Count: {input.Count}");
            context.Logger.Log($"Max value: {input.MaxValue}");

            var random = new Random();
            var nums = new List<int>();
            for (int i = 0; i < input.Count; i++)
            {
                nums.Add(random.Next(input.MaxValue));
            }

            return nums;
        }

        public class RandomsInput
        {
            public int Count { get; set; }
            public int MaxValue { get; set; }
        }
    }
}