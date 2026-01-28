using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace TestCustomAuthorizerApp;

/// <summary>
/// Custom Lambda Authorizer that validates requests and returns context values.
/// This authorizer is configured for HTTP API (API Gateway V2) with simple response format.
/// </summary>
public class AuthorizerFunction
{
    /// <summary>
    /// HTTP API Lambda Authorizer (Payload format 2.0 with simple response)
    /// Returns authorized status along with custom context that can be accessed via [FromCustomAuthorizer]
    /// </summary>
    public APIGatewayCustomAuthorizerV2SimpleResponse HttpApiAuthorize(
        APIGatewayCustomAuthorizerV2Request request, 
        ILambdaContext context)
    {
        context.Logger.LogLine($"Authorizer invoked for path: {request.RawPath}");
        if (request.Headers != null)
        {
            context.Logger.LogLine($"Request headers: {string.Join(", ", request.Headers.Keys)}");
        }
        
        // In a real application, you would validate a token here
        // For this demo, we always authorize and return test context values
        
        // Check for a demo "Authorization" header
        var hasAuthHeader = request.Headers?.ContainsKey("Authorization") == true;
        
        if (hasAuthHeader)
        {
            var authValue = request.Headers!["Authorization"];
            context.Logger.LogLine($"Authorization header value: {authValue}");
            
            // Demo: if the token is "deny", reject the request
            if (authValue.Equals("deny", StringComparison.OrdinalIgnoreCase))
            {
                context.Logger.LogLine("Denying request based on 'deny' token");
                return new APIGatewayCustomAuthorizerV2SimpleResponse
                {
                    IsAuthorized = false
                };
            }
        }
        
        // Return authorized with context values that will be passed to the Lambda function
        // These values can be accessed using [FromCustomAuthorizer(Name = "key")]
        context.Logger.LogLine("Authorizing request with custom context values");
        
        return new APIGatewayCustomAuthorizerV2SimpleResponse
        {
            IsAuthorized = true,
            Context = new Dictionary<string, object>
            {
                // These values will be available via [FromCustomAuthorizer]
                { "userId", "user-12345" },
                { "tenantId", 42 },
                { "userRole", "admin" },
                { "email", "test@example.com" }
            }
        };
    }
}
