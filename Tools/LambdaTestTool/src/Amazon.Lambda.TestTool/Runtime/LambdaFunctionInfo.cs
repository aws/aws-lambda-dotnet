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
        /// http path for the lambda function
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// The Lambda function method name.
        /// </summary>
        public string Method { get; set; }
    }
}