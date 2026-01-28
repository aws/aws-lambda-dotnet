using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.Core;

namespace TestCustomAuthorizerApp;

/// <summary>
/// Lambda functions that demonstrate the [FromCustomAuthorizer] attribute.
/// These functions are protected by the custom authorizer and receive context values
/// that were set by the authorizer.
/// </summary>
public class ProtectedFunction
{
    /// <summary>
    /// Protected endpoint that extracts user information from the custom authorizer context.
    /// The authorizer sets userId, tenantId, userRole, and email in the context.
    /// </summary>
    [LambdaFunction(ResourceName = "ProtectedEndpoint")]
    [HttpApi(LambdaHttpMethod.Get, "/api/protected")]
    public string GetProtectedData(
        [FromCustomAuthorizer(Name = "userId")] string userId,
        [FromCustomAuthorizer(Name = "tenantId")] int tenantId,
        [FromCustomAuthorizer(Name = "userRole")] string userRole,
        ILambdaContext context)
    {
        context.Logger.LogLine($"Request authorized for user: {userId}");
        context.Logger.LogLine($"Tenant ID: {tenantId}");
        context.Logger.LogLine($"User Role: {userRole}");
        
        return $"Hello {userId}! You are a {userRole} in tenant {tenantId}.";
    }

    /// <summary>
    /// Another protected endpoint showing different usage - just getting the email.
    /// </summary>
    [LambdaFunction(ResourceName = "GetUserInfo")]
    [HttpApi(LambdaHttpMethod.Get, "/api/user-info")]
    public object GetUserInfo(
        [FromCustomAuthorizer(Name = "userId")] string userId,
        [FromCustomAuthorizer(Name = "email")] string email,
        [FromCustomAuthorizer(Name = "tenantId")] int tenantId,
        ILambdaContext context)
    {
        context.Logger.LogLine($"Getting user info for: {userId}");
        
        // Return a JSON object with user information
        return new 
        {
            UserId = userId,
            Email = email,
            TenantId = tenantId,
            Message = "This data came from the custom authorizer context!"
        };
    }

    /// <summary>
    /// Simple health check endpoint (no authorizer required for comparison)
    /// </summary>
    [LambdaFunction(ResourceName = "HealthCheck")]
    [HttpApi(LambdaHttpMethod.Get, "/api/health")]
    public string HealthCheck(ILambdaContext context)
    {
        context.Logger.LogLine("Health check called");
        return "OK";
    }
}
