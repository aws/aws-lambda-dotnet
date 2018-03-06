using System;
using Xunit;
using TestFunctionFSharp;
using Amazon.Lambda.TestUtilities;
using Microsoft.FSharp.Core;

namespace TestFunctionCSharp
{
    public class CSharpClassUsingFSharpTypeTest
    {
        [Fact]
        public void GetPaymentMethod_should_return_None_when_given_Garbage()
        {
            var context = new TestLambdaContext();
            // because it's impossible to pass null in F#
            FunctionFSharp.PaymentMethod input = null;
            var actual = FunctionFSharp.getPaymentMethod(input,context);
            Assert.Equal(FSharpOption<FunctionFSharp.PaymentMethod>.None, actual);
        }
    }
}
