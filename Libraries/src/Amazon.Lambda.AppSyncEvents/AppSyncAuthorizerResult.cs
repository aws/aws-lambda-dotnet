using System.Collections.Generic;
using System.Runtime.Serialization;


namespace Amazon.Lambda.AppSyncEvents
{
        /// <summary>
        /// Represents the result returned by an AWS AppSync Lambda authorizer.
        /// </summary>
        [DataContract]
        public class AppSyncAuthorizerResult
        {
                /// <summary>
                /// Indicates if the request is authorized
                /// </summary>
                [DataMember(Name = "isAuthorized")]
#if NETCOREAPP3_1_OR_GREATER
                [System.Text.Json.Serialization.JsonPropertyName("isAuthorized")]
#endif
                public bool IsAuthorized { get; set; }

                /// <summary>
                /// Custom context to pass to resolvers, only supports key-value pairs.
                /// </summary>
                [DataMember(Name = "resolverContext")]
#if NETCOREAPP3_1_OR_GREATER
                [System.Text.Json.Serialization.JsonPropertyName("resolverContext")]
#endif
                public Dictionary<string, string> ResolverContext { get; set; }

                /// <summary>
                /// List of fields that are denied access
                /// </summary>
                [DataMember(Name = "deniedFields")]
#if NETCOREAPP3_1_OR_GREATER
                [System.Text.Json.Serialization.JsonPropertyName("deniedFields")]
#endif
                public IEnumerable<string> DeniedFields { get; set; }

                /// <summary>
                /// The number of seconds that the response should be cached for
                /// </summary>
                [DataMember(Name = "ttlOverride")]
#if NETCOREAPP3_1_OR_GREATER
                [System.Text.Json.Serialization.JsonPropertyName("ttlOverride")]
#endif
                public int? TtlOverride { get; set; }
        }
}
