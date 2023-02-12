namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-verify-auth-challenge-response.html
    /// </summary>
    public class CognitoVerifyAuthChallengeEvent : CognitoTriggerEvent<CognitoVerifyAuthChallengeRequest, CognitoVerifyAuthChallengeResponse>
    {
    }
}
