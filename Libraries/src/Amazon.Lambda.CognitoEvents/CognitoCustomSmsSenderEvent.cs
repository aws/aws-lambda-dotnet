namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-custom-sms-sender.html
    /// </summary>
    public class CognitoCustomSmsSenderEvent : CognitoTriggerEvent<CognitoCustomSmsSenderRequest, CognitoCustomSmsSenderResponse>
    {
    }
}
