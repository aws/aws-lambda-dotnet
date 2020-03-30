namespace Amazon.Lambda.APIGatewayEvents
{
    using System.Collections.Generic;

    /// <summary>
    /// An object representing an IAM policy.
    /// </summary>
    public class APIGatewayCustomAuthorizerPolicy
    {
        /// <summary>
        /// Gets or sets the IAM API version.
        /// </summary>
#if NETCOREAPP_3_1
        [System.Text.Json.Serialization.JsonPropertyName("Version")]
#endif
        public string Version { get; set; } = "2012-10-17";

        /// <summary>
        /// Gets or sets a list of IAM policy statements to apply.
        /// </summary>
#if NETCOREAPP_3_1
        [System.Text.Json.Serialization.JsonPropertyName("Statement")]
#endif
        public List<IAMPolicyStatement> Statement { get; set; } = new List<IAMPolicyStatement>();

        /// <summary>
        /// A class representing an IAM Policy Statement.
        /// </summary>
        public class IAMPolicyStatement
        {
            /// <summary>
            /// Gets or sets the effect the statement has.
            /// </summary>
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("Effect")]
#endif
            public string Effect { get; set; } = "Allow";

            /// <summary>
            /// Gets or sets the action/s the statement has.
            /// </summary>
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("Action")]
#endif
            public HashSet<string> Action { get; set; }

            /// <summary>
            /// Gets or sets the resources the statement applies to.
            /// </summary>
#if NETCOREAPP_3_1
            [System.Text.Json.Serialization.JsonPropertyName("Resource")]
#endif
            public HashSet<string> Resource { get; set; }
        }
    }
}
