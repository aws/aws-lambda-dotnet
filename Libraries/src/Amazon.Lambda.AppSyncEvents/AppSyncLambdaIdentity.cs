namespace Amazon.Lambda.AppSyncEvents;

/// <summary>
/// Represents AWS Lambda authorization identity for AppSync
/// </summary>
public class AppSyncLambdaIdentity
{
    /// <summary>
    /// Optional context information that will be passed to subsequent resolvers
    /// Can contain user information, claims, or any other contextual data
    /// </summary>
    public Dictionary<string, string> ResolverContext { get; set; }
}
