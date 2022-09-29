using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using System.Text.Json.Serialization;

[assembly: LambdaSerializer(typeof(SourceGeneratorLambdaJsonSerializer<SourceGeneratorExample.CustomTypesSerializerContext>))]

namespace SourceGeneratorExample;

[JsonSerializable(typeof(Input))]
[JsonSerializable(typeof(Output))]
public partial class CustomTypesSerializerContext : JsonSerializerContext
{
}


public class Input
{
    public string Name { get; set; }
}

public class Output
{
    public string Response { get; set;}
}

public class Function
{

    public Output FunctionHandler(Input request, ILambdaContext context)
    {
        return new Output { Response = "Response = " + request.Name};
    }
}
