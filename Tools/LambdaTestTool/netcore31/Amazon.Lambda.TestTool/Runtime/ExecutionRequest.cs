namespace Amazon.Lambda.TestTool.Runtime
{
    /// <summary>
    /// The information used to execute the Lambda function within the test tool
    /// </summary>
    public class ExecutionRequest
    {
        /// <summary>
        /// The container for the holds a reference to the code executed for the Lambda function.
        /// </summary>
        public LambdaFunction Function { get; set; }

        /// <summary>
        /// The AWS region that the AWS_REGION environment variable is set to so the AWS SDK for .NET will pick up.
        /// </summary>
        public string AWSRegion { get; set; }

        /// <summary>
        /// The AWS profile that the AWS_PROFILE environment variable is set to so the AWS SDK for .NET will pick up and use for credentials.
        /// </summary>
        public string AWSProfile { get; set; }
        
        /// <summary>
        /// The JSON payload that will be the input of the Lambda function.
        /// </summary>
        public string Payload { get; set; }
        
    }
}