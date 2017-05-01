# Amazon.Lambda.TestUtilities

Package includes test implementation of the interfaces from Amazon.Lambda.Core and helper methods to help in locally testing.

 
 ## Example xUnit test case using Amazon.Lambda.TestUtilities

The test case creates an instance of [TestLambdaContext](/Libraries/src/Amazon.Lambda.TestUtilities/TestLambdaContext.cs) for the function to use. 
By default all properties except for the Logger property are set to null. The default Logger will write to the console. Any properties that your function uses will need to be set
on the TestLambdaContext.
```csharp
[Fact]
public void TestToUpperFunction()
{

    // Invoke the lambda function and confirm the string was upper cased.
    var function = new Function();
    var context = new TestLambdaContext()
    {
        FunctionName = "ToUpper"
    };
    var upperCase = function.FunctionHandler("hello world", context);

    Assert.Equal("HELLO WORLD", upperCase);
}
```
