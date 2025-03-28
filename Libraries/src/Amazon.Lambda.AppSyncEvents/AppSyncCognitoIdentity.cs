namespace Amazon.Lambda.AppSyncEvents;

/// <summary>
/// Represents Amazon Cognito User Pools authorization identity for AppSync
/// </summary>
public class AppSyncCognitoIdentity
{
    /// <summary>
    /// The source IP address of the caller received by AWS AppSync
    /// </summary>
    public List<string> SourceIp { get; set; }

    /// <summary>
    /// The username of the authenticated user
    /// </summary>
    public string Username { get; set; }

    /// <summary>
    /// The UUID of the authenticated user
    /// </summary>
    public string Sub { get; set; }

    /// <summary>
    /// The claims that the user has
    /// </summary>
    public Dictionary<string, object> Claims { get; set; }

    /// <summary>
    /// The default authorization strategy for this caller (ALLOW or DENY)
    /// </summary>
    public string DefaultAuthStrategy { get; set; }

    /// <summary>
    /// List of OIDC groups
    /// </summary>
    public List<string> Groups { get; set; }

    /// <summary>
    /// The token issuer
    /// </summary>
    public string Issuer { get; set; }
}
