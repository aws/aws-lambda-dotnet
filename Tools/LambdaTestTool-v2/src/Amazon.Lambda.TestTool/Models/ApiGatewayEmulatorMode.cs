namespace Amazon.Lambda.TestTool.Models;

/// <summary>
/// Represents the different API Gateway modes.
/// </summary>
public enum ApiGatewayEmulatorMode
{
    /// <summary>
    /// Represents the REST API Gateway mode.
    /// </summary>
    Rest,

    /// <summary>
    /// Represents the HTTP API v1 Gateway mode.
    /// </summary>
    HttpV1,

    /// <summary>
    /// Represents the HTTP API v2 Gateway mode.
    /// </summary>
    HttpV2
}