using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.CamelCaseLambdaJsonSerializer))]

namespace AddFunctionClassLibrary;

public class Function
{
    /// <summary>
    /// Adds the two path parameters {x} and {y} and returns the sum.
    /// Handler string: AddFunctionClassLibrary::AddFunctionClassLibrary.Function::Add
    /// </summary>
    public int Add(APIGatewayHttpApiV2ProxyRequest request, ILambdaContext context)
    {
        var x = int.Parse(request.PathParameters["x"]);
        var y = int.Parse(request.PathParameters["y"]);
        return x + y;
    }
}
