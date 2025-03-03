using Amazon.Lambda.Core;
using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace TestHostBuilderApp;

public class HostBuilderFunctions
{
    private ICalculatorService _calculatorService;

    public HostBuilderFunctions(ICalculatorService calculatorService)
    {
        _calculatorService = calculatorService;
    }

    [LambdaFunction()]
    [HttpApi(LambdaHttpMethod.Get, "/add/{x}/{y}")]
    public int Add(int x, int y, ILambdaContext context)
    {
        var sum = _calculatorService.Add(x, y);
        return sum;
    }
}
