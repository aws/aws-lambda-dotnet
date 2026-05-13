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
        [System.Text.Json.Serialization.JsonPropertyName("Version")]
        public string Version { get; set; } = "2012-10-17";

        /// <summary>
        /// Gets or sets a list of IAM policy statements to apply.
        /// </summary>
        [System.Text.Json.Serialization.JsonPropertyName("Statement")]
        public List<IAMPolicyStatement> Statement { get; set; } = new List<IAMPolicyStatement>();

        /// <summary>
        /// A class representing an IAM Policy Statement.
        /// </summary>
        public class IAMPolicyStatement
        {
            /// <summary>
            /// Gets or sets the effect the statement has.
            /// </summary>
            [System.Text.Json.Serialization.JsonPropertyName("Effect")]
            public string Effect { get; set; } = "Allow";

            /// <summary>
            /// Gets or sets the action/s the statement has.
            /// </summary>
            [System.Text.Json.Serialization.JsonPropertyName("Action")]
            public HashSet<string> Action { get; set; }

            /// <summary>
            /// Gets or sets the resources the statement applies to.
            /// </summary>
            [System.Text.Json.Serialization.JsonPropertyName("Resource")]
            public HashSet<string> Resource { get; set; }

            /// <summary>
            /// Gets or sets the resources the statement does not apply to.
            /// </summary>
            [System.Text.Json.Serialization.JsonPropertyName("NotResource")]
            public HashSet<string> NotResource { get; set; }

            /// <summary>
            /// Gets or sets the conditions for when a policy is in effect. 
            /// https://docs.aws.amazon.com/IAM/latest/UserGuide/reference_policies_elements_condition.html
            /// </summary>
            [System.Text.Json.Serialization.JsonPropertyName("Condition")]
            public IDictionary<string, IDictionary<string, object>> Condition { get; set; }
        }
    }
}
