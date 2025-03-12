using System.Collections.Generic;
#if NETSTANDARD_2_0
using Newtonsoft.Json;
#else
using System.Text.Json.Serialization;
#endif

namespace Amazon.Lambda.AppSyncEvents
{
    public class AppSyncAuthorizerResult
    {
        // Indicates if the request is authorized
#if NETSTANDARD_2_0
        [JsonProperty("isAuthorized")]
#else
        [JsonPropertyName("isAuthorized")]
#endif
        public bool IsAuthorized { get; set; }

        // Custom context to pass to resolvers, only supports key-value pairs.
#if NETSTANDARD_2_0
        [JsonProperty("resolverContext")]
#else
        [JsonPropertyName("resolverContext")]
#endif
        public Dictionary<string, string> ResolverContext { get; set; }

        // List of fields that are denied access
#if NETSTANDARD_2_0
        [JsonProperty("deniedFields")]
#else
        [JsonPropertyName("deniedFields")]
#endif
        public IEnumerable<string> DeniedFields { get; set; }

        // The number of seconds that the response should be cached for
#if NETSTANDARD_2_0
        [JsonProperty("ttlOverride")]
#else
        [JsonPropertyName("ttlOverride")]
#endif
        public int? TtlOverride { get; set; }
    }
}
