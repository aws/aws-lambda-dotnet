using Amazon.Lambda.Core;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace BlueprintBaseName._1;

/// <summary>
/// A collection of sample Lambda functions that provide a REST api for doing simple math calculations. 
/// </summary>
public class Functions
{
    private ICalculatorService _calculatorService;

    /// <summary>
    /// Default constructor.
    /// </summary>
    /// <remarks>
    /// The <see cref="ICalculatorService"/> implementation that we
    /// instantiated in <see cref="Startup"/> will be injected here.
    /// 
    /// As an alternative, a dependency could be injected into each 
    /// Lambda function handler via the [FromServices] attribute.
    /// </remarks>
    public Functions(ICalculatorService calculatorService)
    {
        _calculatorService = calculatorService;
    }

    /// <summary>
    /// Root route that provides information about the other requests that can be made.
    /// </summary>
    /// <returns>API descriptions.</returns>
    [LambdaFunction()]
    [HttpApi(LambdaHttpMethod.Get, "/")]
    public string Default()
    {
        var docs = @"Lambda Calculator Home:
You can make the following requests to invoke other Lambda functions perform calculator operations:
/add/{x}/{y}
/subtract/{x}/{y}
/multiply/{x}/{y}
/divide/{x}/{y}
";
        return docs;
    }

    /// <summary>
    /// Perform x + y
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>Sum of x and y.</returns>
    [LambdaFunction()]
    [HttpApi(LambdaHttpMethod.Get, "/add/{x}/{y}")]
    public int Add(int x, int y, ILambdaContext context)
    {
        var sum = _calculatorService.Add(x, y);

        context.Logger.LogInformation($"{x} plus {y} is {sum}");
        return sum;
    }

    /// <summary>
    /// Perform x - y.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>x subtract y</returns>
    [LambdaFunction()]
    [HttpApi(LambdaHttpMethod.Get, "/subtract/{x}/{y}")]
    public int Subtract(int x, int y, ILambdaContext context)
    {
        var difference = _calculatorService.Subtract(x, y);

        context.Logger.LogInformation($"{x} subtract {y} is {difference}");
        return difference;
    }

    /// <summary>
    /// Perform x * y.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>x multiply y</returns>
    [LambdaFunction()]
    [HttpApi(LambdaHttpMethod.Get, "/multiply/{x}/{y}")]
    public int Multiply(int x, int y, ILambdaContext context)
    {
        var product = _calculatorService.Multiply(x, y);

        context.Logger.LogInformation($"{x} multiplied by {y} is {product}");
        return product;
    }

    /// <summary>
    /// Perform x / y.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <returns>x divide y</returns>
    [LambdaFunction()]
    [HttpApi(LambdaHttpMethod.Get, "/divide/{x}/{y}")]
    public int Divide(int x, int y, ILambdaContext context)
    {
        var quotient = _calculatorService.Divide(x, y);

        context.Logger.LogInformation($"{x} divided by {y} is {quotient}");
        return quotient;
    }
}