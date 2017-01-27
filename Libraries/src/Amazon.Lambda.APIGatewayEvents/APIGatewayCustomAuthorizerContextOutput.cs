namespace Amazon.Lambda.APIGatewayEvents
{
    using System.Runtime.Serialization;

    /// <summary>
    /// An object representing the expected format of an API Gateway custom authorizer response.
    /// </summary>
    [DataContract]
    public class APIGatewayCustomAuthorizerContextOutput
    {
        /// <summary>
        /// Gets or sets the 'stringKey' property.
        /// </summary>
        [DataMember(Name = "stringKey", IsRequired = false)]
        public string StringKey { get; set; }

        /// <summary>
        /// Gets or sets the 'numKey' property.
        /// </summary>
        [DataMember(Name = "numKey", IsRequired = false)]
        public int? NumKey { get; set; }

        /// <summary>
        /// Gets or sets the 'boolKey' property.
        /// </summary>
        [DataMember(Name = "boolKey", IsRequired = false)]
        public bool? BoolKey { get; set; }
    }
}
