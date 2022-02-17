using Xunit;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;


namespace BlueprintBaseName._1.Tests;

public class FunctionTest
{
    [Fact]
    public void TestToUpperFunction()
    {

        // Invoke the lambda function and confirm the string was upper cased.
        var context = new TestLambdaContext();
        var upperCase = Function.FunctionHandler("hello world", context);

        Assert.Equal("HELLO WORLD", upperCase);
    }
}