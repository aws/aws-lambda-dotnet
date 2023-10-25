﻿using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-post-authentication.html
    /// </summary>
    [DataContract]
    public class CognitoPostAuthenticationRequest : CognitoTriggerRequest
    {
        /// <summary>
        /// One or more name-value pairs containing the validation data in the request to register a user. The validation data is set and then passed from the client in the request to register a user. You can pass this data to your Lambda function by using the ClientMetadata parameter in the InitiateAuth and AdminInitiateAuth API actions.
        /// </summary>
        [DataMember(Name = "validationData")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("validationData")]
# endif
        public Dictionary<string, string> ValidationData { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// This flag indicates if the user has signed in on a new device. It is set only if the remembered devices value of the user pool is set to Always or User Opt-In.
        /// </summary>
        [DataMember(Name = "newDeviceUsed")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("newDeviceUsed")]
#endif
        public bool NewDevicedUsed { get; set; }

        /// <summary>
        /// One or more key-value pairs that you can provide as custom input to the Lambda function that you specify for the pre sign-up trigger. You can pass this data to your Lambda function by using the ClientMetadata parameter in the following API actions: AdminCreateUser, AdminRespondToAuthChallenge, ForgotPassword, and SignUp.
        /// </summary>
        [DataMember(Name = "clientMetadata")]
#if NETCOREAPP3_1_OR_GREATER
        [System.Text.Json.Serialization.JsonPropertyName("clientMetadata")]
# endif
        public Dictionary<string, string> ClientMetadata { get; set; } = new Dictionary<string, string>();
    }
}
