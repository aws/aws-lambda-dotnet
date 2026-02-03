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
    // Valid tokens that will be authorized with full context (for testing purposes)
    private static readonly HashSet<string> ValidTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "allow",
        "Bearer allow",
        "valid-token",
        "Bearer valid-token"
    };

    // Special tokens that authorize but return incomplete context (for testing .tt template's 401 handling)
    // These tokens will authorize the request but omit expected context keys like "userId", "tenantId", etc.
    // This allows testing the generated Lambda handler's defensive check for missing authorizer context keys
    private static readonly HashSet<string> PartialContextTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "partial-context",
        "Bearer partial-context"
    };

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
        
        // Check for Authorization header
        // Note: HTTP API v2 lowercases all header names
        var hasAuthHeader = request.Headers?.ContainsKey("authorization") == true;
        
        if (hasAuthHeader)
        {
            var authValue = request.Headers!["authorization"];
            context.Logger.LogLine($"Authorization header value: {authValue}");
            
            // Check for partial-context tokens first - these authorize but return incomplete context
            // This tests the .tt template's defensive 401 handling when expected context keys are missing
            if (PartialContextTokens.Contains(authValue))
            {
                context.Logger.LogLine("Authorizing request with partial-context token (missing expected keys)");
                
                return new APIGatewayCustomAuthorizerV2SimpleResponse
                {
                    IsAuthorized = true,
                    Context = new Dictionary<string, object>
                    {
                        // Intentionally return only unexpected keys, omitting userId, tenantId, email, etc.
                        // This will cause the generated Lambda handler to return 401 when it tries to
                        // extract [FromCustomAuthorizer] values that don't exist
                        { "unexpectedKey", "some-value" }
                    }
                };
            }

            // Authorize with full context for valid tokens
            if (ValidTokens.Contains(authValue))
            {
                context.Logger.LogLine("Authorizing request with valid token");
                
                // Return authorized with context values that will be passed to the Lambda function
                // These values can be accessed using [FromCustomAuthorizer(Name = "key")]
                return new APIGatewayCustomAuthorizerV2SimpleResponse
                {
                    IsAuthorized = true,
                    Context = new Dictionary<string, object>
                    {
                        // These values will be available via [FromCustomAuthorizer]
                        { "userId", "user-12345" },
                        { "tenantId", "42" },
                        { "userRole", "admin" },
                        { "email", "test@example.com" },
                        // Non-string types for testing type conversion
                        { "numericTenantId", 42 },      // int
                        { "isAdmin", true },            // bool
                        { "score", 95.5 }               // double
                    }
                };
            }
        }
        
        // Deny by default - no valid token found
        context.Logger.LogLine("Denying request - no valid token found");
        return new APIGatewayCustomAuthorizerV2SimpleResponse
        {
            IsAuthorized = false
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

        var authToken = request.AuthorizationToken ?? "";

        // Deny if not a valid or partial-context token
        if (!ValidTokens.Contains(authToken) && !PartialContextTokens.Contains(authToken))
        {
            context.Logger.LogLine("Denying request - no valid token found");
            return GenerateDenyPolicy("user", request.MethodArn);
        }

        // Parse the method ARN to create a policy
        // Format: arn:aws:execute-api:{region}:{accountId}:{apiId}/{stage}/{method}/{resourcePath}
        var arnParts = request.MethodArn.Split(':');
        var apiGatewayArnPart = arnParts[5].Split('/');
        var region = arnParts[3];
        var accountId = arnParts[4];
        var apiId = apiGatewayArnPart[0];
        var stage = apiGatewayArnPart[1];
        var resourceArn = $"arn:aws:execute-api:{region}:{accountId}:{apiId}/{stage}/*";

        // Check for partial-context tokens - authorize but return incomplete context
        if (PartialContextTokens.Contains(authToken))
        {
            context.Logger.LogLine("Authorizing request with partial-context token (missing expected keys)");
            
            return new APIGatewayCustomAuthorizerResponse
            {
                PrincipalID = "partial-user",
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
                    // Intentionally return only unexpected keys, omitting userId, tenantId, email
                    ["unexpectedKey"] = "some-value"
                }
            };
        }

        context.Logger.LogLine("Authorizing request with valid token");
        
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
