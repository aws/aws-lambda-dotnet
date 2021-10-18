using System;
using System.Numerics;
using Amazon.Lambda.Annotations;

namespace TestServerlessApp
{
    public class ComplexCalculator
    {
        [LambdaFunction]
        [HttpApi(HttpApiVersion.V2)]
        public Tuple<double, double> Add()
        {
            var c1 = new Complex(4, 2);
            var c2 = new Complex(2, 4);
            var result = c1 + c2;
            return new Tuple<double, double>(result.Real, result.Imaginary);
        }
    }
}