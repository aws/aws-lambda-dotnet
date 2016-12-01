using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.Lambda.Core;

namespace Amazon.Lambda.TestUtilities
{
    /// <summary>
    /// A test implementation of the ILambdaContext interface used for writing local tests of Lambda Functions.
    /// </summary>
    public class TestLambdaContext : ILambdaContext
    {
        /// <summary>
        /// The AWS request ID associated with the request.
        /// </summary>
        public string AwsRequestId { get; set; }

        /// <summary>
        /// Information about the client application and device when invoked
        /// through the AWS Mobile SDK.
        /// </summary>
        public IClientContext ClientContext { get; set; }

        /// <summary>
        /// Name of the Lambda function that is running.
        /// </summary>
        public string FunctionName { get; set; }

        /// <summary>
        /// The Lambda function version that is executing.
        /// If an alias is used to invoke the function, then this will be
        /// the version the alias points to.
        /// </summary>
        public string FunctionVersion { get; set; }

        /// <summary>
        /// Information about the Amazon Cognito identity provider when
        /// invoked through the AWS Mobile SDK.
        /// </summary>
        public ICognitoIdentity Identity { get; set; }

        /// <summary>
        /// The ARN used to invoke this function.
        /// </summary>
        public string InvokedFunctionArn { get; set; }

        /// <summary>
        /// Lambda logger associated with the Context object. For the TestLambdaContext this is default to the TestLambdaLogger.
        /// </summary>
        public ILambdaLogger Logger { get; set; } = new TestLambdaLogger();

        /// <summary>
        /// The CloudWatch log group name associated with the invoked function.
        /// </summary>
        public string LogGroupName { get; set; }

        /// <summary>
        /// The CloudWatch log stream name for this function execution.
        /// </summary>
        public string LogStreamName { get; set; }

        /// <summary>
        /// Memory limit, in MB, you configured for the Lambda function.
        /// </summary>
        public int MemoryLimitInMB { get; set; }

        /// <summary>
        /// Remaining execution time till the function will be terminated.
        /// </summary>
        public TimeSpan RemainingTime { get; set; }
    }
}
