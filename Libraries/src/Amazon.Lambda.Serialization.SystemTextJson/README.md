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
