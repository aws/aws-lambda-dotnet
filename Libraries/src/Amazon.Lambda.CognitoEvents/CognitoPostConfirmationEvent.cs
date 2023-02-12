using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-post-confirmation.html
    /// </summary>
    [DataContract]
    public class CognitoPostConfirmationEvent : CognitoTriggerEvent<CognitoPostConfirmationRequest, CognitoPostConfirmationResponse>
    {
    }
}
