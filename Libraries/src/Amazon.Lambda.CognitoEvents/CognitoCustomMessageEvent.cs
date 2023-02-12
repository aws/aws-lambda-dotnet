namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-custom-message.html
    /// </summary>
    public class CognitoCustomMessageEvent : CognitoTriggerEvent<CognitoCustomMessageRequest, CognitoCustomMessageResponse>
    {
    }
}
