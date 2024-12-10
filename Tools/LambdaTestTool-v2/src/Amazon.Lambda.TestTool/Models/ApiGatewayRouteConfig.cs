namespace Amazon.Lambda.TestTool.Models;

/// <summary>
/// Represents the configuration of a Lambda function
/// </summary>
public class ApiGatewayRouteConfig
{
    /// <summary>
    /// The name of the Lambda function
    /// </summary>
    public required string LambdaResourceName { get; set; }
    
    /// <summary>
    /// The endpoint of the local Lambda Runtime API
    /// </summary>
    public string? Endpoint { get; set; }
    
    /// <summary>
    /// The HTTP Method for the API Gateway endpoint
    /// </summary>
    public required string HttpMethod { get; set; }
    
    /// <summary>
    /// The API Gateway HTTP Path of the Lambda function
    /// </summary>
    public required string Path { get; set; }
}