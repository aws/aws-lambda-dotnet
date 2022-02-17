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
        var function = new Function();
        var context = new TestLambdaContext();
        var casing = function.FunctionHandler("hello world", context);

        Assert.Equal("hello world", casing.Lower);
        Assert.Equal("HELLO WORLD", casing.Upper);
    }
}