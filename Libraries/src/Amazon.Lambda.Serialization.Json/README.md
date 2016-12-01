# Amazon.Lambda.Serialization.Json

This package contains a custom `Amazon.Lambda.Core.ILambdaSerializer` implementation which uses Newtonsoft.Json 9.0.1 to serialize/deserialize .NET types in Lambda functions.

This serializer can be present on the assembly or on the handler method. If you specify both, the method attribute takes priority.

Here is an example of setting this attribute on the assembly.
```
[assembly: Amazon.Lambda.Core.LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
```

And this is how the method can be applied to the handler method.
```
[Amazon.Lambda.Core.LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
public Response CustomSerializerMethod(Request input)
{
    ...
}
```
