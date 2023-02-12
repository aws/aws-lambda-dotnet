using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-pre-sign-up.html
    /// </summary>
    [DataContract]
    public class CognitoPreSignupResponse : CognitoTriggerResponse
    {
        /// <summary>
        /// Set to true to auto-confirm the user, or false otherwise.
        /// </summary>
        [DataMember(Name = "autoConfirmUser")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("autoConfirmUser")]
#endif
        public bool AutoConfirmUser { get; set; }

        /// <summary>
        /// Set to true to set as verified the email of a user who is signing up, or false otherwise. If autoVerifyEmail is set to true, the email attribute must have a valid, non-null value. Otherwise an error will occur and the user will not be able to complete sign-up.
        /// </summary>
        [DataMember(Name = "autoVerifyPhone")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("autoVerifyPhone")]
#endif
        public bool AutoVerifyPhone { get; set; }

        /// <summary>
        /// Set to true to set as verified the phone number of a user who is signing up, or false otherwise. If autoVerifyPhone is set to true, the phone_number attribute must have a valid, non-null value. Otherwise an error will occur and the user will not be able to complete sign-up.
        /// </summary>
        [DataMember(Name = "autoVerifyEmail")]
#if NETCOREAPP3_1
        [System.Text.Json.Serialization.JsonPropertyName("autoVerifyEmail")]
#endif
        public bool AutoVerifyEmail { get; set; }
    }
}
