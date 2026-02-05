using System;
using System.Collections.Generic;

namespace Amazon.Lambda.TestTool.Runtime
{
    public class LambdaFunctionInfo
    {
        /// <summary>
        /// Display friendly name of the Lambda Function.
        /// </summary>
        public string Name { get; set; }
        
        /// <summary>
        /// The Lambda function handler string.
        /// </summary>
        public string Handler { get; set; }

        /// <summary>
        /// The amount of time the lambda function has to run before it times out.
        /// </summary>
        public TimeSpan Timeout { get; set; }

        public IDictionary<string, string> EnvironmentVariables { get; }  = new Dictionary<string, string>();
    }
}