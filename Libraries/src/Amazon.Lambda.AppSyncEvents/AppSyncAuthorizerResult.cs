using System.Text.Json.Serialization;
namespace Amazon.Lambda.AppSyncEvents;

/// <summary>
/// Represents the authorization result returned by a Lambda authorizer to AWS AppSync 
/// containing authorization decisions and optional context for the GraphQL API.
/// </summary>
public class AppSyncAuthorizerResult
{
    /// <summary>
    /// Indicates if the request is authorized
    /// </summary>
    [JsonPropertyName("isAuthorized")]
    public bool IsAuthorized { get; set; }

    /// <summary>
    /// Custom context to pass to resolvers, only supports key-value pairs.
    /// </summary>
    [JsonPropertyName("resolverContext")]
    public Dictionary<string, string> ResolverContext { get; set; }

    /// <summary>
    /// List of fields that are denied access
    /// </summary>
    [JsonPropertyName("deniedFields")]
    public IEnumerable<string> DeniedFields { get; set; }

    /// <summary>
    /// The number of seconds that the response should be cached for
    /// </summary>
    [JsonPropertyName("ttlOverride")]
    public int? TtlOverride { get; set; }
}
