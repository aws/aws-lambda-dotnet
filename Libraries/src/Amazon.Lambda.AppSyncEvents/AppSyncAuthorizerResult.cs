using System.Collections.Generic;

namespace Amazon.Lambda.AppSyncEvents
{
    public class AppSyncAuthorizerResult
    {
        // Indicates if the request is authorized
        public bool IsAuthorized { get; set; }
        // Custom context to pass to resolvers, only supports key-value pairs.
        public Dictionary<string, string> ResolverContext { get; set; }
        // List of fields that are denied access
        public IEnumerable<string> DeniedFields { get; set; }
        // The number of seconds that the response should be cached for
        public int? TtlOverride { get; set; }
    }
}
