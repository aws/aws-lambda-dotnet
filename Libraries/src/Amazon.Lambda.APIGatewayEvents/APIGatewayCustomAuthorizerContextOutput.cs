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
        public string StringKey
        {
            get
            {
                object value;
                if (this.TryGetValue("stringKey", out value))
                    return value.ToString();
                return null;
            }
            set
            {
                this["stringKey"] = value;
            }
        }

        /// <summary>
        /// Gets or sets the 'numKey' property.
        /// </summary>
        [Obsolete("This property is obsolete. Code should be updated to use the string index property like authorizer[\"numKey\"]")]
        public int? NumKey
        {
            get
            {
                object value;
                if (this.TryGetValue("numKey", out value))
                {
                    int i;
                    if (int.TryParse(value?.ToString(), out i))
                    {
                        return i;
                    }
                }

                return null;
            }
            set
            {
                this["numKey"] = value;
            }
        }

        /// <summary>
        /// Gets or sets the 'boolKey' property.
        /// </summary>
        [Obsolete("This property is obsolete. Code should be updated to use the string index property like authorizer[\"boolKey\"]")]
        public bool? BoolKey
        {
            get
            {
                object value;
                if (this.TryGetValue("boolKey", out value))
                {
                    bool b;
                    if (bool.TryParse(value?.ToString(), out b))
                    {
                        return b;
                    }
                }

                return null;
            }
            set
            {
                this["boolKey"] = value;
            }
        }
    }
}
