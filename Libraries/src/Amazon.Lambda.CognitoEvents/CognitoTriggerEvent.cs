using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// AWS Cognito Trigger Common Parameters
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/cognito-user-identity-pools-working-with-aws-lambda-triggers.html#cognito-user-pools-lambda-trigger-syntax-shared
    /// </summary>
    [DataContract]
    public abstract class CognitoTriggerEvent<TRequest, TResponse>
        where TRequest : CognitoTriggerRequest, new()
        where TResponse : CognitoTriggerResponse, new()
    {
        /// <summary>
        /// The version number of your Lambda function.
        /// </summary>
        [DataMember(Name = "version")]
        [System.Text.Json.Serialization.JsonPropertyName("version")]
        public string Version { get; set; }

        /// <summary>
        /// The AWS Region, as an AWSRegion instance.
        /// </summary>
        [DataMember(Name = "region")]
        [System.Text.Json.Serialization.JsonPropertyName("region")]
        public string Region { get; set; }

        /// <summary>
        /// The user pool ID for the user pool.
        /// </summary>
        [DataMember(Name = "userPoolId")]
        [System.Text.Json.Serialization.JsonPropertyName("userPoolId")]
        public string UserPoolId { get; set; }

        /// <summary>
        /// The username of the current user.
        /// </summary>
        [DataMember(Name = "userName")]
        [System.Text.Json.Serialization.JsonPropertyName("userName")]
        public string UserName { get; set; }

        /// <summary>
        /// The caller context
        /// </summary>
        [DataMember(Name = "callerContext")]
        [System.Text.Json.Serialization.JsonPropertyName("callerContext")]
        public CognitoTriggerCallerContext CallerContext { get; set; } = new CognitoTriggerCallerContext();

        /// <summary>
        /// The name of the event that triggered the Lambda function.For a description of each triggerSource see User pool Lambda trigger sources.
        /// </summary>
        [DataMember(Name = "triggerSource")]
        [System.Text.Json.Serialization.JsonPropertyName("triggerSource")]
        public string TriggerSource { get; set; }

        /// <summary>
        /// The request from the Amazon Cognito service
        /// </summary>
        [DataMember(Name = "request")]
        [System.Text.Json.Serialization.JsonPropertyName("request")]
        public TRequest Request { get; set; } = new TRequest();

        /// <summary>
        /// The response from your Lambda trigger.The return parameters in the response depend on the triggering event.
        /// </summary>
        [DataMember(Name = "response")]
        [System.Text.Json.Serialization.JsonPropertyName("response")]
        public TResponse Response { get; set; } = new TResponse();
    }
}
