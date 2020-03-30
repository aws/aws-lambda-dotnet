namespace Amazon.Lambda.APIGatewayEvents
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.Serialization;

#if NETSTANDARD_2_0
    using Newtonsoft.Json.Linq;
#else
    using System.Text.Json;
#endif


    /// <summary>
    /// An object representing the expected format of an API Gateway custom authorizer response.
    /// </summary>
    [DataContract]
    public class APIGatewayCustomAuthorizerContext : Dictionary<string, object>
    {
        /// <summary>
        /// Gets or sets the 'principalId' property.
        /// </summary>
        [Obsolete("This property is obsolete. Code should be updated to use the string index property like authorizer[\"principalId\"]")]
        public string PrincipalId
        {
            get
            {
                object value;
                if (this.TryGetValue("principalId", out value))
                    return value.ToString();
                return null;
            }
            set
            {
                this["principalId"] = value;
            }
        }

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
                    if(bool.TryParse(value?.ToString(), out b))
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

        Dictionary<string, string> _claims;
        /// <summary>
        /// Gets or sets the claims coming from Cognito
        /// </summary>
        public Dictionary<string, string> Claims
        {
            get
            {
                if(_claims == null)
                {
                    _claims = new Dictionary<string, string>();

                    object value;
                    if(this.TryGetValue("claims", out value))
                    {
#if NETSTANDARD_2_0
                        JObject jsonClaims = value as JObject;
                        if (jsonClaims != null)
                        {
                            foreach (JProperty property in jsonClaims.Properties())
                            {
                                _claims[property.Name] = property.Value?.ToString();

                            }
                        }
#else
                        if(value is JsonElement jsonClaims)
                        {
                            foreach(JsonProperty property in jsonClaims.EnumerateObject())
                            {
                                if(property.Value.ValueKind == JsonValueKind.String)
                                {
                                    _claims[property.Name] = property.Value.GetString();
                                }
                            }
                        }
#endif
                    }
                }

                return _claims;
            }
            set
            {
                this._claims = value;
            }
        }
    }
}
