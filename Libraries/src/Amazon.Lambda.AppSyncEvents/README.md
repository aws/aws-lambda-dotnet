# Amazon.Lambda.AppSyncEvents

This package contains classes that can be used as input types for Lambda functions that process AppSync events.

# Sample Function

The following is a sample class and Lambda function that receives AppSync event record data as an `appSyncEvent` and logs some of the incoming event data. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

```csharp
public void FunctionHandler(AppSyncEvent appSyncEvent, ILambdaContext context)
{
    var input = JsonSerializer.Serialize(appSyncEvent.Arguments);
    Console.WriteLine($"AppSync request payload {input}.");

    if (appSyncEvent.Identity != null)
    {
        var lambdaSerializer = new DefaultLambdaJsonSerializer();

        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(appSyncEvent.Identity.ToString()!)))
        {
            // When using AMAZON_COGNITO_USER_POOLS authorization
            var cognitoIdentity = lambdaSerializer.Deserialize<AppSyncIdentityCognito>(stream);

            // When using AWS_IAM authorization
            var iamIdentity = lambdaSerializer.Deserialize<AppSyncIdentityIAM>(stream);
        }
    }
}
```
