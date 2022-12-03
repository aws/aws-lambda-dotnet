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

        public IDictionary<string, string> EnvironmentVariables { get; }  = new Dictionary<string, string>();
    }
}