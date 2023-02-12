namespace Amazon.Lambda.CognitoEvents.LambdaTriggerEvents.Base
{
    /// <summary>
    /// Amazon Cognito passes event information to your Lambda function which returns the same event object back to Amazon Cognito with any changes in the response.
    /// </summary>
    public abstract class TriggerEvent
    {
        /// <summary>
        /// The version number of your Lambda function.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// The name of the event that triggered the Lambda function.
        /// For a description of each triggerSource see User Pool Lambda Trigger Sources.
        /// </summary>
        public string TriggerSource { get; set; }

        /// <summary>
        /// The AWS Region, as an AWSRegion instance.
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// The user pool ID for the user pool.
        /// </summary>
        public string UserPoolId { get; set; }

        /// <summary>
        /// The username of the current user.
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// The caller contex
        /// </summary>
        public CallerContext CallerContext { get; set; }
    }
}