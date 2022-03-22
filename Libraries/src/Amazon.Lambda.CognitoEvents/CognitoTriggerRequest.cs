using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/cognito-user-identity-pools-working-with-aws-lambda-triggers.html#cognito-user-pools-lambda-trigger-syntax-shared
    /// </summary>
    [DataContract]
    public abstract class CognitoTriggerRequest
    {
        /// <summary>
        /// One or more pairs of user attribute names and values.Each pair is in the form "name": "value".
        /// </summary>
        [DataMember(Name = "userAttributes")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("userAttributes")]
#endif
        public Dictionary<string, string> UserAttributes { get; set; } = new Dictionary<string, string>();
    }
}
