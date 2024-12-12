namespace Amazon.Lambda.TestTool.Models;

/// <summary>
/// The type of API Gateway Route. This is used to determine the priority of the route when there is route overlap.
/// </summary>
public enum ApiGatewayRouteType
{
    /// <summary>
    /// An exact route with no path variables.
    /// </summary>
    Exact = 0,
    
    /// <summary>
    /// A route with path variables, but not a greedy variable {proxy+}.
    /// </summary>
    Variable = 1,
    
    /// <summary>
    /// A route with a greedy path variables.
    /// </summary>
    Proxy = 2
}