using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-pre-authentication.html
    /// </summary>
    [DataContract]
    public class CognitoPreAuthenticationResponse : CognitoTriggerResponse
    {
    }
}
