# Amazon.Lambda.Serialization.SystemTextJson

This package contains a custom `Amazon.Lambda.Core.ILambdaSerializer` implementation which uses System.Text.Json to 
serialize/deserialize .NET types in Lambda functions. This serializer targets .NET Core 3.1 so can not be used with 
the .NET Core 2.1 Lambda runtime.

If targeting .NET Core 3.1 this serializer is highly recommend over Amazon.Lambda.Serialization.Json and can significantly reduce
cold start performance in Lambda.

This serializer can be present on the assembly or on the handler method. If you specify both, the method attribute takes priority.

Here is an example of setting this attribute on the assembly.
```
[assembly: Amazon.Lambda.Core.LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
```

And this is how the method can be applied to the handler method.
```csharp
[Amazon.Lambda.Core.LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]
public Response CustomSerializerMethod(Request input)
{
    ...
}
```

## Using C# source generator for serialization

C# 9 provides source generators, which allow code generation during compilation. This can reduce 
the use of reflection APIs and improve application startup time. .NET 6 updated the native 
JSON library [System.Text.Json to use source generators](https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-source-generation?pivots=dotnet-6-0), 
allowing JSON parsing without requiring reflection APIs.

To use the source generator for JSON serializing in Lambda, you must define a new empty class 
in your project that derives from `System.Text.Json.Serialization.JsonSerializerContext`. This 
class must be a partial class because the source generator adds code to this class to handle 
serialization. On the empty partial class, add the `JsonSerializable` attribute for each .NET 
type the source generator must generate the serialization code for.

Here is an example called HttpApiJsonSerializerContext that registers the Amazon API Gateway 
HTTP API event and response types to have the serialization code generated:

```csharp
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyRequest))]
[JsonSerializable(typeof(APIGatewayHttpApiV2ProxyResponse))]
public partial class HttpApiJsonSerializerContext : JsonSerializerContext
{
}
```

To register the source generator for serialization use the `LambdaSerializer` attribute 
passing in the `SourceGeneratorLambdaJsonSerializer` type along with the custom context 
type (i.e. `HttpApiJsonSerializerContext`) as the generic parameter.

```csharp
[assembly: LambdaSerializer(
        typeof(SourceGeneratorLambdaJsonSerializer
                    <APIGatewayExampleImage.HttpApiJsonSerializerContext>))]
```

## Customizing serialization options

Both `DefaultLambdaJsonSerializer` and `SourceGeneratorLambdaJsonSerializer` construct an 
instance of `JsonSerializerOptions` that is used to customize the serialization and deserialization 
of the Lambda JSON events. For example adding special converters and naming policies.

To further customize the `JsonSerializerOptions` create a new type extending from extend 
either `DefaultLambdaJsonSerializer` or `SourceGeneratorLambdaJsonSerializer` and pass 
in an `Action` customizer to the base constructor. Then register the new type as the 
serializer using the `LambdaSerializer` attribute. Below is an example of a custom serializer.

```csharp
public class CustomLambdaSerializer : DefaultLambdaJsonSerializer
{
    public CustomLambdaSerializer()
        : base(CreateCustomizer())
    { }

    private static Action<JsonSerializerOptions> CreateCustomizer()
    {
        return (JsonSerializerOptions options) =>
        {
            // Customize options
        };
    }
}
```