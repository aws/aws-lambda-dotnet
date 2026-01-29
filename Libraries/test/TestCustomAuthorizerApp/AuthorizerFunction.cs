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
        // Note: HTTP API v2 lowercases all header names
        var hasAuthHeader = request.Headers?.ContainsKey("authorization") == true;
        
        if (hasAuthHeader)
        {
            var authValue = request.Headers!["authorization"];
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
                { "tenantId", "42" },
                { "userRole", "admin" },
                { "email", "test@example.com" }
            }
        };
    }

    /// <summary>
    /// REST API Lambda Authorizer (Token-based authorizer)
    /// Returns an IAM policy document along with custom context values
    /// </summary>
    public APIGatewayCustomAuthorizerResponse RestApiAuthorize(
        APIGatewayCustomAuthorizerRequest request,
        ILambdaContext context)
    {
        context.Logger.LogLine($"REST API Authorizer invoked");
        context.Logger.LogLine($"Authorization token: {request.AuthorizationToken}");
        context.Logger.LogLine($"Method ARN: {request.MethodArn}");

        // In a real application, you would validate the token here
        // For this demo, we authorize unless the token is "deny"
        
        if (string.Equals(request.AuthorizationToken, "deny", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.AuthorizationToken, "Bearer deny", StringComparison.OrdinalIgnoreCase))
        {
            context.Logger.LogLine("Denying request based on 'deny' token");
            return GenerateDenyPolicy("user", request.MethodArn);
        }

        context.Logger.LogLine("Authorizing request with custom context values");
        
        // Parse the method ARN to create a policy
        // Format: arn:aws:execute-api:{region}:{accountId}:{apiId}/{stage}/{method}/{resourcePath}
        var arnParts = request.MethodArn.Split(':');
        var apiGatewayArnPart = arnParts[5].Split('/');
        var region = arnParts[3];
        var accountId = arnParts[4];
        var apiId = apiGatewayArnPart[0];
        var stage = apiGatewayArnPart[1];

        // Create policy allowing all methods on this API
        var resourceArn = $"arn:aws:execute-api:{region}:{accountId}:{apiId}/{stage}/*";
        
        return new APIGatewayCustomAuthorizerResponse
        {
            PrincipalID = "user-12345",
            PolicyDocument = new APIGatewayCustomAuthorizerPolicy
            {
                Version = "2012-10-17",
                Statement = new List<APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement>
                {
                    new APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement
                    {
                        Action = new HashSet<string> { "execute-api:Invoke" },
                        Effect = "Allow",
                        Resource = new HashSet<string> { resourceArn }
                    }
                }
            },
            Context = new APIGatewayCustomAuthorizerContextOutput
            {
                // REST API context values are automatically converted to strings
                ["userId"] = "user-12345",
                ["tenantId"] = "42",
                ["userRole"] = "admin", 
                ["email"] = "test@example.com"
            }
        };
    }

    private APIGatewayCustomAuthorizerResponse GenerateDenyPolicy(string principalId, string methodArn)
    {
        return new APIGatewayCustomAuthorizerResponse
        {
            PrincipalID = principalId,
            PolicyDocument = new APIGatewayCustomAuthorizerPolicy
            {
                Version = "2012-10-17",
                Statement = new List<APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement>
                {
                    new APIGatewayCustomAuthorizerPolicy.IAMPolicyStatement
                    {
                        Action = new HashSet<string> { "execute-api:Invoke" },
                        Effect = "Deny",
                        Resource = new HashSet<string> { methodArn }
                    }
                }
            }
        };
    }
}
