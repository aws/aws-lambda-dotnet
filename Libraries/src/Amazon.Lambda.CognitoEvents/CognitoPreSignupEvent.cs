using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-pre-sign-up.html
    /// </summary>
    [DataContract]
    public class CognitoPreSignupEvent : CognitoTriggerEvent<CognitoPreSignupRequest, CognitoPreSignupResponse>
    {
    }
}
