namespace Amazon.Lambda.APIGatewayEvents
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    /// <summary>
    /// An object representing the expected format of an API Gateway custom authorizer response.
    /// </summary>
    [DataContract]
    public class APIGatewayCustomAuthorizerContextOutput : Dictionary<string, object>
    {
        /// <summary>
        /// Gets or sets the 'stringKey' property.
        /// </summary>
        [Obsolete("This property is obsolete. Code should be updated to use the string index property like authorizer[\"stringKey\"]")]
        public string StringKey { get; set; }

        /// <summary>
        /// Gets or sets the 'numKey' property.
        /// </summary>
        [Obsolete("This property is obsolete. Code should be updated to use the string index property like authorizer[\"numKey\"]")]
        public int? NumKey { get; set; }

        /// <summary>
        /// Gets or sets the 'boolKey' property.
        /// </summary>
        [Obsolete("This property is obsolete. Code should be updated to use the string index property like authorizer[\"boolKey\"]")]
        public bool? BoolKey { get; set; }
    }
}
