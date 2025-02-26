# Amazon.Lambda.AppSyncEvents

This package contains classes that can be used as input types for Lambda functions that process AppSync events.

# Sample Function

The following is a sample class and Lambda function that receives AppSync event record data as an `appSyncResolverEvent` and logs some of the incoming event data. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```csharp
public void Handler(AppSyncResolverEvent<Dictionary<string, object>> appSyncResolverEvent, ILambdaContext context)
{
    foreach (var item in appSyncResolverEvent.Arguments)
    {
        Console.WriteLine($"AppSync request key - {item.Key}.");
    }

    if (appSyncResolverEvent.Identity != null)
    {
        // Create an instance of the serializer
        var lambdaSerializer = new DefaultLambdaJsonSerializer();

        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(appSyncResolverEvent.Identity.ToString()!)))
        {
            // When using AMAZON_COGNITO_USER_POOLS authorization
            var cognitoIdentity = lambdaSerializer.Deserialize<AppSyncCognitoIdentity>(stream);

            // When using AWS_IAM authorization
            var iamIdentity = lambdaSerializer.Deserialize<AppSyncIamIdentity>(stream);
        }
    }
}
```
