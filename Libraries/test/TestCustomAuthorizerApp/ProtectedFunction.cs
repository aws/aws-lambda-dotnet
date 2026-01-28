using Amazon.Lambda.Annotations;
using Amazon.Lambda.Annotations.APIGateway;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using System.Text.Json;

namespace TestCustomAuthorizerApp;

/// <summary>
/// Lambda functions that demonstrate the [FromCustomAuthorizer] attribute.
/// These functions are protected by the custom authorizer and receive context values
/// that were set by the authorizer.
/// </summary>
public class ProtectedFunction
{
    /// <summary>
    /// Debug endpoint to see what's in the RequestContext.Authorizer
    /// </summary>
    [LambdaFunction(ResourceName = "ProtectedEndpoint")]
    [HttpApi(LambdaHttpMethod.Get, "/api/protected")]
    public string GetProtectedData(
        APIGatewayHttpApiV2ProxyRequest request,
        ILambdaContext context)
    {
        // Debug: Log the entire authorizer context
        context.Logger.LogLine("=== DEBUG: Checking RequestContext.Authorizer ===");
        
        if (request.RequestContext?.Authorizer == null)
        {
            context.Logger.LogLine("RequestContext.Authorizer is NULL");
            return "ERROR: Authorizer context is null";
        }
        
        context.Logger.LogLine($"Authorizer object exists");
        
        // Check the Lambda dictionary specifically
        if (request.RequestContext.Authorizer.Lambda == null)
        {
            context.Logger.LogLine("RequestContext.Authorizer.Lambda is NULL");
        }
        else
        {
            context.Logger.LogLine($"Lambda dictionary has {request.RequestContext.Authorizer.Lambda.Count} entries");
            foreach (var kvp in request.RequestContext.Authorizer.Lambda)
            {
                context.Logger.LogLine($"  Lambda[\"{kvp.Key}\"] = {kvp.Value} (Type: {kvp.Value?.GetType().Name ?? "null"})");
            }
        }
        
        // Check JWT authorizer
        if (request.RequestContext.Authorizer.Jwt != null)
        {
            context.Logger.LogLine("JWT authorizer context found");
        }
        
        // Log the raw JSON for the full request context
        try
        {
            var authorizerJson = JsonSerializer.Serialize(request.RequestContext.Authorizer);
            context.Logger.LogLine($"Full Authorizer JSON: {authorizerJson}");
        }
        catch (Exception ex)
        {
            context.Logger.LogLine($"Error serializing authorizer: {ex.Message}");
        }
        
        // Try to get context values if Lambda dictionary exists
        if (request.RequestContext.Authorizer.Lambda != null)
        {
            var userId = request.RequestContext.Authorizer.Lambda.ContainsKey("userId") 
                ? request.RequestContext.Authorizer.Lambda["userId"]?.ToString() 
                : "NOT_FOUND";
            var tenantId = request.RequestContext.Authorizer.Lambda.ContainsKey("tenantId")
                ? request.RequestContext.Authorizer.Lambda["tenantId"]?.ToString()
                : "NOT_FOUND";
            var userRole = request.RequestContext.Authorizer.Lambda.ContainsKey("userRole")
                ? request.RequestContext.Authorizer.Lambda["userRole"]?.ToString()
                : "NOT_FOUND";
            
            return $"Found context - userId: {userId}, tenantId: {tenantId}, userRole: {userRole}";
        }
        
        return "Lambda authorizer context not found in request";
    }

    /// <summary>
    /// Another protected endpoint showing different usage - just getting the email.
    /// </summary>
    [LambdaFunction(ResourceName = "GetUserInfo")]
    [HttpApi(LambdaHttpMethod.Get, "/api/user-info")]
    public object GetUserInfo(
        [FromCustomAuthorizer(Name = "userId")] string userId,
        [FromCustomAuthorizer(Name = "email")] string email,
        [FromCustomAuthorizer(Name = "tenantId")] string tenantId,
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

    /// <summary>
    /// REST API endpoint demonstrating [FromCustomAuthorizer] with REST API authorizer.
    /// REST API authorizers use a different context structure than HTTP API v2.
    /// </summary>
    [LambdaFunction(ResourceName = "RestUserInfo")]
    [RestApi(LambdaHttpMethod.Get, "/api/rest-user-info")]
    public object GetRestUserInfo(
        [FromCustomAuthorizer(Name = "userId")] string userId,
        [FromCustomAuthorizer(Name = "email")] string email,
        [FromCustomAuthorizer(Name = "tenantId")] string tenantId,
        ILambdaContext context)
    {
        context.Logger.LogLine($"[REST API] Getting user info for: {userId}");
        
        // Return a JSON object with user information
        return new 
        {
            UserId = userId,
            Email = email,
            TenantId = tenantId,
            ApiType = "REST API",
            Message = "This data came from the REST API custom authorizer context!"
        };
    }
}
