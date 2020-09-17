namespace Amazon.Lambda.CognitoEvents.LambdaTriggerEvents.Base
{
    public abstract class TriggerEvent
    {
        public string Version { get; set; }
        public string TriggerSource { get; set; }
        public string Region { get; set; }
        public string UserPoolId { get; set; }
        public string UserName { get; set; }
        public string CallerContext { get; set; }
        public TriggerRequest Request { get; set; }
        public TriggerResponse Response { get; set; }
    }
}