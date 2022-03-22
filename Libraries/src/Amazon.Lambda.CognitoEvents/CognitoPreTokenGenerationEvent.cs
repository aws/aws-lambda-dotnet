namespace Amazon.Lambda.CognitoEvents
{
    /// <summary>
    /// https://docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-pre-token-generation.html
    /// </summary>
    public class CognitoPreTokenGenerationEvent : CognitoTriggerEvent<CognitoPreTokenGenerationRequest, CognitoPreTokenGenerationResponse>
    {
    }
}
