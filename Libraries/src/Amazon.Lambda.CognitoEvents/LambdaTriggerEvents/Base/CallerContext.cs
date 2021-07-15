namespace Amazon.Lambda.CognitoEvents.LambdaTriggerEvents.Base
{
    /// <summary>
    /// The caller contex
    /// </summary>
    public class CallerContext
    {
        /// <summary>
        /// The AWS SDK version number.
        /// </summary>
        public string AwsSdkVersion { get; set; }

        /// <summary>
        /// The ID of the client associated with the user pool.
        /// </summary>
        public string ClientId { get; set; }
    }
}