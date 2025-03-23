# Amazon.Lambda.AppSyncEvents

This package contains classes that can be used as input types for Lambda functions that process AppSync events.

## Sample Function

The following is a sample class and Lambda function that receives AppSync resolver event record data as an `appSyncResolverEvent` and logs some of the incoming event data. (Note that by default anything written to Console will be logged as CloudWatch Logs events.)

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

            // When using AWS_LAMBDA authorization
            var lambdaIdentity = lambdaSerializer.Deserialize<AppSyncLambdaIdentity>(stream);

            // When using OPENID_CONNECT authorization
            var oidcIdentity = lambdaSerializer.Deserialize<AppSyncOidcIdentity>(stream);
        }
    }
}
```

## Example of Custom Lambda Authorizer
This example demonstrates how to implement a custom Lambda authorizer for AppSync using the AppSync Events package. The authorizer function receives an `AppSyncAuthorizerEvent` containing the authorization token and request context. It returns an `AppSyncAuthorizerResult` that determines whether the request is authorized and includes additional context.

The function also provides some data in the `resolverContext` object. This information is available in the AppSync resolver’s context `identity` object.

```
public async Task<AppSyncAuthorizerResult> CustomLambdaAuthorizerHandler(AppSyncAuthorizerEvent appSyncAuthorizerEvent)
{
    var authorizationToken = appSyncAuthorizerEvent.AuthorizationToken;
    var apiId = appSyncAuthorizerEvent.RequestContext.ApiId;
    var accountId = appSyncAuthorizerEvent.RequestContext.AccountId;

    var response = new AppSyncAuthorizerResult
    {
        IsAuthorized = authorizationToken == "custom-authorized",
        ResolverContext = new Dictionary<string, string>
        {
            { "userid", "test-user-id" },
            { "info", "contextual information A" },
            { "more_info", "contextual information B" }
        },
        DeniedFields = new List<string>
        {
            $"arn:aws:appsync:{Environment.GetEnvironmentVariable("AWS_REGION")}:{accountId}:apis/{apiId}/types/Event/fields/comments",
            "Mutation.createEvent"
        },
        TtlOverride = 10
    };

    return response;
}
```