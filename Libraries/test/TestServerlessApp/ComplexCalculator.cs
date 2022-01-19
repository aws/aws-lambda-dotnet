using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

namespace TestServerlessApp
{
    public class ComplexCalculator
    {
        [LambdaFunction(PackageType = LambdaPackageType.Image)]
        [HttpApi(LambdaHttpMethod.Post, "/ComplexCalculator/Add")]
        public Tuple<double, double> Add([FromBody]string complexNumbers, ILambdaContext context, APIGatewayHttpApiV2ProxyRequest request)
        {
            context.Logger.Log($"Request {JsonSerializer.Serialize(request)}");

            var components = complexNumbers.Split(";");
            if (components.Length != 2)
            {
                throw new ArgumentException(@$"Complex numbers must be in format ""1,2;3,4"", but found ""{complexNumbers}""");
            }

            var firstComponent = components[0].Split(",");
            if (firstComponent.Count() != 2)
            {
                throw new ArgumentException(@$"Complex number must be in format ""1,2"", but found ""{firstComponent}""");
            }

            var secondComponent = components[1].Split(",");
            if (secondComponent.Count() != 2)
            {
                throw new ArgumentException(@$"Complex number must be in format ""1,2"", but found ""{secondComponent}""");
            }

            var c1 = new Complex(int.Parse(firstComponent[0]), int.Parse(firstComponent[1]));
            var c2 = new Complex(int.Parse(secondComponent[0]), int.Parse(secondComponent[1]));
            var result = c1 + c2;
            return new Tuple<double, double>(result.Real, result.Imaginary);
        }

        [LambdaFunction(PackageType = LambdaPackageType.Image)]
        [HttpApi(LambdaHttpMethod.Post, "/ComplexCalculator/Subtract")]
        public Tuple<double, double> Subtract([FromBody]IList<IList<int>> complexNumbers)
        {
            if (complexNumbers.Count() != 2)
            {
                throw new ArgumentException("There must be two complex numbers");
            }

            var firstComponent = complexNumbers[0];
            if (firstComponent.Count() != 2)
            {
                throw new ArgumentException(@$"Complex number must be in format [1,2], but found ""{firstComponent}""");
            }

            var secondComponent = complexNumbers[1];
            if (secondComponent.Count() != 2)
            {
                throw new ArgumentException(@$"Complex number must be in format [1,2], but found ""{secondComponent}""");
            }

            var c1 = new Complex(firstComponent[0], firstComponent[1]);
            var c2 = new Complex(secondComponent[0], secondComponent[1]);
            var result = c1 - c2;
            return new Tuple<double, double>(result.Real, result.Imaginary);
        }
    }

}