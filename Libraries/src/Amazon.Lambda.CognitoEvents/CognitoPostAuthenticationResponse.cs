using System.Runtime.Serialization;

namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-post-authentication.html
    /// </summary>
    [DataContract]
    public class CognitoPostAuthenticationResponse : CognitoTriggerResponse
    {
    }
}
